# Changelog

## [1.1.0](https://github.com/ChurroStack/churrostack/compare/v1.0.4...v1.1.0) (2026-05-26)


### Features

* **applications:** filter sidebar by environment and creator ([#17](https://github.com/ChurroStack/churrostack/issues/17)) ([0ba9a82](https://github.com/ChurroStack/churrostack/commit/0ba9a82dd86515de5298f7d7580f60052189584a))
* enrich Environment Usage tab with status, P95, sorting, totals ([#16](https://github.com/ChurroStack/churrostack/issues/16)) ([543e2f0](https://github.com/ChurroStack/churrostack/commit/543e2f076c5407b56b3f4463645e2fda62cf9343))
* **llm:** add USD spend tracking and filters to monitoring ([#18](https://github.com/ChurroStack/churrostack/issues/18)) ([94576b2](https://github.com/ChurroStack/churrostack/commit/94576b271b974be74525d6ff1986ccf027e8651f))

## [1.0.4](https://github.com/ChurroStack/churrostack/compare/v1.0.3...v1.0.4) (2026-05-25)


### Bug Fixes

* **monitoring:** bucket metrics in user TZ + neutral empty state ([#14](https://github.com/ChurroStack/churrostack/issues/14)) ([960c009](https://github.com/ChurroStack/churrostack/commit/960c009f3a879896d21b4e9d927107793f37aa49))

## [1.0.3](https://github.com/ChurroStack/churrostack/compare/v1.0.2...v1.0.3) (2026-05-25)


### Bug Fixes

* **api:** truncate metric tables in RetypeCpuGpuMetricsAsGauge ([#12](https://github.com/ChurroStack/churrostack/issues/12)) ([0602891](https://github.com/ChurroStack/churrostack/commit/06028913fe45e49b0bcb01fc2fbec909cfdfe5dd))

## [1.0.2](https://github.com/ChurroStack/churrostack/compare/v1.0.1...v1.0.2) (2026-05-25)


### Bug Fixes

* **ui:** track apps/ui/.env so prod build has OIDC clientId ([#10](https://github.com/ChurroStack/churrostack/issues/10)) ([c3e690d](https://github.com/ChurroStack/churrostack/commit/c3e690d68507f6f307e0360813e71ccfdbc6ce1d))

## [1.0.1](https://github.com/ChurroStack/churrostack/compare/v1.0.0...v1.0.1) (2026-05-25)


### Bug Fixes

* unblock API boot — install GSSAPI lib and raise migration timeout ([#8](https://github.com/ChurroStack/churrostack/issues/8)) ([d495f57](https://github.com/ChurroStack/churrostack/commit/d495f578c82beec96afdc6e390a460c9a6789ec8))

## 1.0.0 (2026-05-25)


### Features

* Application Size Recommendation ([#5](https://github.com/ChurroStack/churrostack/issues/5)) ([c8d20a7](https://github.com/ChurroStack/churrostack/commit/c8d20a7d1dfea8c434a0215c4f426bce958bb930))
* per-app Auto-Start and Auto-Stop for /share/* requests ([#7](https://github.com/ChurroStack/churrostack/issues/7)) ([a74939d](https://github.com/ChurroStack/churrostack/commit/a74939de93627155a21e4a3fde15adc41108756d))
* runtime CPU/Memory quota enforcement + app env context in UI ([#6](https://github.com/ChurroStack/churrostack/issues/6)) ([96a7600](https://github.com/ChurroStack/churrostack/commit/96a76003edc8d1ad63b793c508f4e247368d87be))


### Bug Fixes

* correct CPU metric type and Rate() calculation ([#2](https://github.com/ChurroStack/churrostack/issues/2)) ([1aec43c](https://github.com/ChurroStack/churrostack/commit/1aec43c32ea2681b4a3bec59d54e40729f8d2d49))
