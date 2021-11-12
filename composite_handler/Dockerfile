FROM python:3.9
LABEL maintainer="Daniel Grant <daniel.grant@digirati.com>"

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

ENV DEBIAN_FRONTEND=noninteractive
ENV PYTHONUNBUFFERED=1

RUN apt-get update && apt-get --yes install apt-utils && apt-get --yes upgrade \
    && apt-get --yes install poppler-data poppler-utils \
    && apt-get --yes autoremove && apt-get --yes autoclean && apt-get --yes clean \
    && useradd --create-home --home-dir /srv/dlcs --shell /bin/bash --uid 1000 dlcs \
    && python -m pip install --upgrade pip

COPY --chown=dlcs:dlcs . /srv/dlcs

USER dlcs
WORKDIR /srv/dlcs

RUN pip install --no-warn-script-location --requirement requirements.txt
