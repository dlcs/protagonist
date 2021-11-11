FROM python:3.9
LABEL maintainer="Daniel Grant <daniel.grant@digirati.com>"

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

ENV PYTHONUNBUFFERED=1

WORKDIR /app
COPY . /app/

RUN pip install -r requirements.txt
