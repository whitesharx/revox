# Revox

Simple continuous integration utility to revoke Unity licenses.

[![Docker](https://img.shields.io/docker/v/whitesharx/revox/latest?color=green&label=docker-hub&logo=docker)](https://hub.docker.com/r/whitesharx/revox)

## How does it work?

Revox offers more rubost approach to deactive unity licenses then `-returnlicense` option. It
simply signs in to your account using headless browser and presses Revoke All button. If you
launch it before every build you wount be having trouble with dangling activations on your account.

> Revox also uses IMAP inbox checking to pass Unity signin email confirmation code. You should setup your
> Unity CI account with service that offers IMAP interface.

# Usage

```bash
# Environment variables that must be defined.
UNITY__LOGIN=unity-account@email.com
UNITY__PASSWORD=unityAccountPassword
EMAIL__LOGIN=unity-account@email.com
EMAIL__PASSWORD=imapMailPassword
```

```bash
# Run container with environment defined.
docker run --rm --env-file .env whitesharx/revox
```

```yaml
# Example with CircleCi configuration that runs before every build.
# Simple job given above env vars devined as Context.

jobs:
  revoke-licenses:
    docker:
      - image: whitesharx/revox
    steps:
      - run:
          name: Revoke all activated Unity licenses
          command: /revox/Revox
```
