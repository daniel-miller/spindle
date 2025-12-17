# config: Project configuration files

Configuration files, environment settings, dependency injection configuration, etc.

## What should go in config?

- Application settings (e.g. `appsettings.json`, `.env`, YAML/JSON config)
- Service and integration configurations
- Settings for all environments (dev/local, test, qa, uat, demo/sandbox/stage, live/prod, preview)

## What should NOT go in config?

- Secrets or credentials (**always** store securely outside repo!)