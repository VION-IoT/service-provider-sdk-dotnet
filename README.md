# Vion.ServiceProvider.Sdk

SDK for building service providers that integrate with the Vion IoT platform. Handles MQTT connection, registration flow, setup schema exchange, message handler registration, and health reporting.

Full documentation: **https://docs.vion.swiss**

## Install

```bash
dotnet add package Vion.ServiceProvider.Sdk
```

Targets `net10.0`. Built on top of [Vion.Contracts](https://github.com/vion-iot/vion-contracts) and [MQTTnet](https://github.com/dotnet/MQTTnet).

## Source-available

This repository is source-available under [Apache 2.0](LICENSE). Issues and pull requests are not accepted from outside the `vion-iot` organization. See [CONTRIBUTING.md](CONTRIBUTING.md), [SUPPORT.md](SUPPORT.md), and [SECURITY.md](SECURITY.md).

Maintainers: the release process lives in [docs/releasing.md](docs/releasing.md).
