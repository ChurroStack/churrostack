# Changelog

## [1.8.1](https://github.com/ChurroStack/churrostack/compare/v1.8.0...v1.8.1) (2026-06-20)


### Bug Fixes

* MySQL/phpMyAdmin template + /share proxy POST forwarding & port re-render ([#48](https://github.com/ChurroStack/churrostack/issues/48)) ([7bb39cd](https://github.com/ChurroStack/churrostack/commit/7bb39cd9c4d89dbedb8135ec95c38ecd8f62565d))

## [1.8.0](https://github.com/ChurroStack/churrostack/compare/v1.7.0...v1.8.0) (2026-06-19)


### Features

* **templates:** add MySQL + phpMyAdmin application template ([#46](https://github.com/ChurroStack/churrostack/issues/46)) ([324350c](https://github.com/ChurroStack/churrostack/commit/324350cc4dfe37d326d7a42c4d8c4b6b716cc9c7))

## [1.7.0](https://github.com/ChurroStack/churrostack/compare/v1.6.0...v1.7.0) (2026-06-16)


### Features

* **llms:** add Peak RPM/TPM charts and usage columns ([#44](https://github.com/ChurroStack/churrostack/issues/44)) ([2370d72](https://github.com/ChurroStack/churrostack/commit/2370d729ce8a9ce709064df3a288d1492a1c2a99))

## [1.6.0](https://github.com/ChurroStack/churrostack/compare/v1.5.0...v1.6.0) (2026-06-11)


### Features

* **ui:** add read-only Resources tab to environment detail ([#42](https://github.com/ChurroStack/churrostack/issues/42)) ([92b3319](https://github.com/ChurroStack/churrostack/commit/92b331947f39a4716d9d368723257dfe537d5af2))

## [1.5.0](https://github.com/ChurroStack/churrostack/compare/v1.4.3...v1.5.0) (2026-06-11)


### Features

* **storage:** multi-mount storage with environment-controlled host paths ([#40](https://github.com/ChurroStack/churrostack/issues/40)) ([3aec807](https://github.com/ChurroStack/churrostack/commit/3aec807acbe6268ba2d87aeb2b4e5a86d5a84487))

## [1.4.3](https://github.com/ChurroStack/churrostack/compare/v1.4.2...v1.4.3) (2026-06-04)


### Bug Fixes

* **api:** stop replaying stale gauge metrics for dead pods ([#39](https://github.com/ChurroStack/churrostack/issues/39)) ([635f51e](https://github.com/ChurroStack/churrostack/commit/635f51ec69fe9cba997d1c201d12178588b1cc3b))
* **ui:** invalidate monitoring widgets on time-range/filter change ([#37](https://github.com/ChurroStack/churrostack/issues/37)) ([e21b12a](https://github.com/ChurroStack/churrostack/commit/e21b12a28ec5f7982dd36aa26d03a01f565f1fd4))

## [1.4.2](https://github.com/ChurroStack/churrostack/compare/v1.4.1...v1.4.2) (2026-05-29)


### Bug Fixes

* **api:** harden scheduler auto-start with longer hold + Polly retry ([#35](https://github.com/ChurroStack/churrostack/issues/35)) ([2e8da74](https://github.com/ChurroStack/churrostack/commit/2e8da74d3fb854c071a2588574f362cd964bf30e))

## [1.4.1](https://github.com/ChurroStack/churrostack/compare/v1.4.0...v1.4.1) (2026-05-28)


### Bug Fixes

* **api:** co-issue cookie session for /share/* and harden proxy/CORS ([#34](https://github.com/ChurroStack/churrostack/issues/34)) ([8d84652](https://github.com/ChurroStack/churrostack/commit/8d846521967f32a5d18f4c887babab8bcc99b6da))
* **ui:** populate identity editor on edit + allow member changes ([#32](https://github.com/ChurroStack/churrostack/issues/32)) ([2973abd](https://github.com/ChurroStack/churrostack/commit/2973abdc57ffce4adb54106ff73f2c33ef734196))

## [1.4.0](https://github.com/ChurroStack/churrostack/compare/v1.3.0...v1.4.0) (2026-05-27)


### Features

* **tags:** filter applications and environments by tags ([#31](https://github.com/ChurroStack/churrostack/issues/31)) ([d7749fa](https://github.com/ChurroStack/churrostack/commit/d7749fa55df614e484d6c668a6a689c3c9ceb90b))
* **ui:** env usage createdBy + LLM skeleton/Empty + fix -$0.00 ([#29](https://github.com/ChurroStack/churrostack/issues/29)) ([02e6084](https://github.com/ChurroStack/churrostack/commit/02e60848191818779dea57a46b5532897afb77c4))

## [1.3.0](https://github.com/ChurroStack/churrostack/compare/v1.2.1...v1.3.0) (2026-05-26)


### Features

* **llms:** aggregated monitoring view + fix negative spend at source ([#28](https://github.com/ChurroStack/churrostack/issues/28)) ([ba55bc2](https://github.com/ChurroStack/churrostack/commit/ba55bc261946c00cab97a9881c3aef5fba0991e4))

## [1.2.1](https://github.com/ChurroStack/churrostack/compare/v1.2.0...v1.2.1) (2026-05-26)


### Bug Fixes

* **ci:** include component+version in release-please title pattern ([#24](https://github.com/ChurroStack/churrostack/issues/24)) ([13b5a47](https://github.com/ChurroStack/churrostack/commit/13b5a47d2f9c9219098771d3af736113f92194da))

## [1.2.0](https://github.com/ChurroStack/churrostack/compare/v1.1.0...v1.2.0) (2026-05-26)


### Features

* **llm:** add Playground tab for chatting via public OpenAI endpoint ([#20](https://github.com/ChurroStack/churrostack/issues/20)) ([0eed53a](https://github.com/ChurroStack/churrostack/commit/0eed53a1f07aa815b0bd5d1eba33838609226235))


### Bug Fixes

* **llm:** price spend for internal:// destinations and ceil sub-cent USD ([#22](https://github.com/ChurroStack/churrostack/issues/22)) ([988145c](https://github.com/ChurroStack/churrostack/commit/988145c847eeb1a0831fd4e2a0a71c0e7d7a563d))

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
