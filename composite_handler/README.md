# DLCS - Composite Handler

The DLCS Composite Handler is an implementation of [RFC011](../docs/rfcs/011-pdfs-as-input.md).

## About

The component is written in Python and utilises Django with the following extensions:

- [Django REST Framework](https://github.com/encode/django-rest-framework/tree/master)
- [Django Q](https://github.com/Koed00/django-q)
- [django-environ](https://github.com/joke2k/django-environ)
- [django-health-check](https://github.com/KristianOellegaard/django-health-check)

Additionally, the project uses:

- [Boto3](https://github.com/boto/boto3)
- [jsonschema](https://github.com/Julian/jsonschema)
- [pdf2image](https://github.com/Belval/pdf2image)
- [requests](https://github.com/psf/requests)
- [tqdm](https://github.com/tqdm/tqdm)

## Getting Started

The project ships with a [`docker-compose.yml`](docker-compose.yml) that can be used to get a local version of the component running:

```bash
docker-compose up
```

Note that for the Composite Handler to be able to interact with the target S3 bucket, the Docker Compose assumes that the `AWS_PROFILE` environment variable has been set and a valid AWS session is available.

This will create a PostgreSQL instance, bootstrap it with the required tables, deploy a single instance of the API, and three instances of the engine. Requests can then be targetted at `localhost:8000`.

The component can also be run directly, either in an IDE or from the CLI. The component must first be configured either via the creation of a `.env` file (see [`.env.dist`](.env.dist) for an example configuration), or via a set of environment variables (see the [Configuration](#configuration) section).

Once configuration is in place, the following commands will start the API and / or engine:

- API: `python manage.py runserver 0.0.0.0:8000`
- Engine: `python manage.py qcluster`

Should the required tables not exist in the target database, the following commands should be run first:

```bash
python manage.py migrate
python manage.py createcachetable
```

Once the API is running, an administrator interface can be accessed via the browser at `http://localhost:8000/admin`. To create an administrator login, run the following command:

```
python manage.py createsuperuser
```

The administrator user can be used to browse the database and manage the queue (including deleting tasks and resubmitting failed tasks into the queue).

## Configuration

The following list of environment variables are supported:

| Environment Variable          | Default Value                  | Component(s) | Description                                                                                                                                                                                                                                                                  |
|-------------------------------|--------------------------------|--------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `DJANGO_DEBUG`                | `True`                         | API, Engine  | Whether Django should run in debug. Useful for development purposes but should be set to `False` in production.                                                                                                                                                              |
| `DJANGO_SECRET_KEY`           | None                           | API, Engine  | The secret key used by Django when generating sensitive tokens. This should a randomly generated 50 character string.                                                                                                                                                        |
| `SCRATCH_DIRECTORY`           | `/tmp/scratch`                 | Engine       | A locally accessible filesystem path where work-in-progress files are written during rasterization.                                                                                                                                                                          |
| `WEB_SERVER_SCHEME`           | `http`                         | API          | The HTTP scheme used when generating URI's.                                                                                                                                                                                                                                  |
| `WEB_SERVER_HOSTNAME`         | `localhost:8000`               | API          | The hostname (and optional port) used when generating URI's.                                                                                                                                                                                                                 |
| `ORIGIN_CHUNK_SIZE`           | `8192`                         | Engine       | The chunk size, in bytes, used when retrieving objects from origins. Tailoring this value can theoretically improve download speeds.                                                                                                                                         |
| `DATABASE_URL`                | None                           | API, Engine  | The URL of the target PostgreSQL database, in a format acceptable to [django-environ](https://django-environ.readthedocs.io/en/latest/getting-started.html#usage), e.g. `postgresql://dlcs:password@postgres:5432/compositedb`.                                              |
| `CACHE_URL`                   | None                           | API, Engine  | The URL of the target cache, in a format acceptable to [django-environ](https://django-environ.readthedocs.io/en/latest/getting-started.html#usage), e.g. `dbcache://app_cache`.                                                                                             |
| `PDF_RASTERIZER_THREAD_COUNT` | `3`                            | Engine       | The number of concurrent [Poppler](https://poppler.freedesktop.org/) threads spawned when a worker is rasterizing a PDF. Each thread typically consumes 100% of a CPU core.                                                                                                  |
| `PDF_RASTERIZER_DPI`          | `500`                          | Engine       | The DPI of images generated during the rasterization process. For JPEG's, the default value of `500` typically produces images approximately 1.5MiB to 2MiB in size.                                                                                                         |
| `PDF_RASTERIZER_FORMAT`       | `jpg`                          | Engine       | The format to generate rasterized images in. Supported values are `ppm`, `jpeg` / `jpg`, `png` and `tiff`                                                                                                                                                                    |
| `DLCS_API_ROOT`               | `https://api.dlcs.digirati.io` | Engine       | The root URI of the API of the target DLCS deployment, without the trailing slash.                                                                                                                                                                                           |
| `DLCS_S3_BUCKET_NAME`         | `dlcs-composite-images`        | Engine       | The S3 bucket that the Composite Handler will push rasterized images to, for consumption by the wider DLCS. Both the Composite Handler and the DLCS must have access to this bucket.                                                                                         |
| `DLCS_S3_OBJECT_KEY_PREFIX`   | `composites`                   | Engine       | The S3 key prefix to use when pushing images to the `DLCS_S3_BUCKET_NAME` - in other words, the folder within the S3 bucket into which images are stored.                                                                                                                    |
| `DLCS_S3_UPLOAD_THREADS`      | `8`                            | Engine       | The number of concurrent threads to use when pushing images to the S3 bucket. A higher number of threads will significantly lower the amount of time spent pushing images to S3, however too high a value will cause issues with Boto3. `8` is a testing and sensible value. |
| `ENGINE_WORKER_COUNT`         | `2`                            | Engine       | The number of workers a single instance of the engine will spawn. Each worker will handle the processing of a single PDF, so the total number of concurrent PDF's that can be processed is `engine_count * worker_count`.                                                    |
| `ENGINE_WORKER_TIMEOUT`       | `3600`                         | Engine       | The number of seconds that a task (i.e. the processing of a single PDF) can run for before being terminated and treated as a failure. This value is useful to purging "stuck" tasks which haven't technically failed but are occupying a worker.                             |
| `ENGINE_WORKER_RETRY`         | `4500`                         | Engine       | The number of seconds since a task was presented for processing before a worker will re-run, regardless of whether it is still running or failed. As such, this value must be higher than `ENGINE_WORKER_TIMEOUT`.                                                           |
| `ENGINE_WORKER_MAX_ATTEMPTS`  | `0`                            | Engine       | The number of processing attempts a single task will undergo before it is abandoned. Setting this value to `0` will cause a task to be retried forever.                                                                                                                      |

Note that in order to access the S3 bucket, the Composite Handler assumes that valid AWS credentials are available in the environment - this can be in the former of [environment variables](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-envvars.html), or in the form of ambient credentials.

## Building

The project ships with a [`Dockerfile.CompositeHandler`](../Dockerfile.CompositeHandler):

```bash
docker build -f ../Dockerfile.CompositeHandler -t dlcs/composite-handler:latest .
```

This will produce a single image that can be used to execute any of the supported Django commands, including running the API and the engine:

```bash
docker run dlcs/composite-handler:latest python manage.py migrate # Apply any pending DB schema changes
docker run dlcs/composite-handler:latest python manage.py createcachetable # Create the cache table (if it doesn't exist)
docker run dlcs/composite-handler:latest python manage.py runserver 0.0.0.0:8000 # Run the API
docker run dlcs/composite-handler:latest python manage.py qcluster # Run the engine
docker run dlcs/composite-handler:latest python manage.py qmonitor # Monitor the workers
```
