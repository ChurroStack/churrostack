# document-converter

Converts uploaded office documents (Word, Excel, PowerPoint, ODF, RTF, CSV,
images, ...) to PDF over a small async HTTP API, backed by headless
LibreOffice (`soffice --headless --convert-to pdf`).

There is no lighter-weight "in-process" conversion path — every option
(`soffice`, `unoserver`, `unoconv`, ...) drives the same LibreOffice engine.
This image shells out to `soffice` per job: simplest to operate, and its
1-2s fork cost is fully hidden behind the async flow below.

## API

All routes are served under the optional `BASE_PATH` prefix (see
[Base path](#base-path-mounting-under-a-sub-path) below); examples here
assume `BASE_PATH` is unset.

### `POST /convert`

Upload a document as multipart form data (`file`). Enqueues a conversion job
and returns immediately.

```bash
curl -F "file=@report.docx" http://localhost:8000/convert
```

```json
{
  "id": "9d23d44e4300464c9f0fa324ac850401",
  "status": "pending",
  "queue_position": 1,
  "status_url": "/convert/9d23d44e4300464c9f0fa324ac850401"
}
```

- `202 Accepted` on success.
- `400` if the upload has no filename.
- `413` if the upload exceeds `MAX_UPLOAD_MB`.

### `GET /convert/{id}`

Poll for the result.

```bash
curl -o report.pdf http://localhost:8000/convert/9d23d44e4300464c9f0fa324ac850401
```

- **`202 Accepted`** + `Retry-After: 2` header, body `{"id","status"}` with
  `status` of `pending` (still queued) or `processing` (converting right
  now) — **not ready yet, retry the request**.
- **`200 OK`** with the PDF as the response body (`Content-Type:
  application/pdf`, `Content-Disposition: attachment; filename="<name>.pdf"`)
  once conversion succeeded. Pass `?meta=1` to get `{"id","status","ready_at"}`
  JSON instead of the binary.
- **`404 Not Found`** if the id is unknown, or the PDF has already expired
  and been swept (see [Lifecycle & cleanup](#lifecycle--cleanup) below) —
  re-submit the document.
- **`500 Internal Server Error`** with `{"id","status":"failed","error"}` if
  the conversion itself failed (e.g. a corrupt/unsupported input).

### `GET /health`

Always served at the bare `/health` (outside `BASE_PATH`, for simple
liveness-probe wiring) — `{"status": "ok"}`.

## Processing model

Jobs are held in an in-memory FIFO queue (`asyncio.Queue`) drained by a
single worker task, so conversions run **strictly in submission order, one
at a time** — never in parallel, never reordered. `queue_position` in the
`POST` response tells you how many jobs are ahead of yours at submit time.

## Lifecycle & cleanup

- As soon as a conversion **succeeds**, the original uploaded file is
  deleted immediately — only the resulting PDF is kept on disk.
- A ready PDF **expires `PDF_TTL` seconds after it becomes ready** (default
  5 minutes) — download it promptly. Expiry is enforced by an in-process
  periodic sweep (an `asyncio` background task, not a system cron) that
  wakes every `SWEEP_INTERVAL` seconds and deletes expired job directories.
  After expiry, `GET /convert/{id}` returns `404`.
- Failed jobs' directories (including the original input, kept for
  inspection) are cleared out on the same TTL.

## Base path mounting under a sub-path

Set `BASE_PATH` to mount every route under a prefix, e.g. so a reverse proxy
can route `/my/path/*` to this container without stripping the prefix:

```bash
docker run -p 8000:8000 -e BASE_PATH=/my/path churrostack/document-converter
```

```bash
curl -F "file=@report.docx" http://localhost:8000/my/path/convert
curl -o report.pdf http://localhost:8000/my/path/convert/<id>
```

Swagger docs move with it too: `http://localhost:8000/my/path/docs`.
`/health` is always unprefixed regardless of `BASE_PATH`.

## Environment variables

| Variable | Default | Meaning |
| --- | --- | --- |
| `BASE_PATH` | *(empty)* | Route prefix, e.g. `/my/path`. Applies to every route except `/health`. |
| `WORK_DIR` | `/app/home/jobs` | Where per-job input/output files are stored. |
| `SOFFICE_BIN` | `soffice` | Path/name of the LibreOffice binary to invoke. |
| `CONVERT_TIMEOUT` | `120` | Max seconds to wait for a single `soffice` conversion before it's marked `failed`. |
| `PDF_TTL` | `300` (5 min) | Seconds after `ready_at` before a completed PDF is expired and deleted. |
| `SWEEP_INTERVAL` | `60` | How often (seconds) the cleanup sweep runs. |
| `MAX_UPLOAD_MB` | `100` | Max accepted upload size in MB (`413` above this). |

## Supported formats

Anything the bundled LibreOffice components (`writer`, `calc`, `impress`,
`draw`) can import: `.doc`/`.docx`, `.xls`/`.xlsx`, `.ppt`/`.pptx`, `.odt`,
`.ods`, `.odp`, `.rtf`, `.csv`, and common image formats, converted to PDF.

## Fonts

Debian `trixie-slim` base with `ttf-mscorefonts-installer` (proprietary
Arial, Times New Roman, Courier New, Verdana, ...) plus
`fonts-crosextra-carlito` / `fonts-crosextra-caladea` — metric-compatible
substitutes for **Calibri** and **Cambria** (the post-2007 Office defaults,
which mscorefonts does *not* include) — so documents authored with common
Office fonts keep their layout and don't silently reflow onto a fallback
font. `fonts-liberation` and Noto CJK/emoji fonts are included for broader
coverage.

## Build

```bash
./build.sh                    # local image document-converter:0.0.1-local, no push
./build.sh <version> --push   # build linux/amd64 and push to quay.io
```

Or directly:

```bash
docker build -t document-converter:local .
docker run --rm -p 8000:8000 document-converter:local
```

## Known limitations

- **Single replica only.** The job registry and queue are in-memory and the
  work directory is process-local; a container restart loses in-flight job
  state, and running multiple replicas behind a load balancer would split
  polling requests across registries that don't share state. Horizontal
  scaling would need an external job store and shared storage — out of
  scope for this image.
- **Serialized conversions.** Only one `soffice` conversion runs at a time
  (by design, to keep a single writable LibreOffice profile lifecycle
  simple); large queues process one document at a time, in order.
- **Narrow expiry race.** The TTL sweep and a `GET` download both touch the
  same file; if a sweep tick lands in the brief window between the
  existence check and the response streaming the PDF, that one download can
  fail. The client's retry then gets a clean `404` (re-submit) rather than a
  silent partial file, so the failure mode is safe, just occasionally an
  extra round trip right at the TTL boundary.
