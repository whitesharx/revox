# Revox

Simple continuous integration utility to revoke Unity licenses

# Usage

```
UNITY__LOGIN=unity-account@email.com
UNITY__PASSWORD=unityAccountPassword
EMAIL__LOGIN=unity-account@mail.com
EMAIL__PASSWORD=imapMailPassword
```

```bash
docker run --rm --env-file .env whitesharx/revox
```
