import logging

import requests
from django.conf import settings

logger = logging.Logger(__name__)


class DLCS:
    def __init__(self):
        self._api_root = settings.DLCS["api_root"].geturl()

    def test_credentials(self, customer, auth):
        response = requests.get(
            f"{self._api_root}customers/{customer}",
            headers={"Content-Type": "application/json", "Authorization": auth},
        )
        if response.status_code == requests.codes.ok:
            return response.json()
        else:
            response.raise_for_status()

    def ingest(self, customer, json, auth):
        response = requests.post(
            f"{self._api_root}customers/{customer}/queue",
            headers={"Content-Type": "application/json", "Authorization": auth},
            json=json,
        )
        if response.status_code == requests.codes.created:
            return response.json()
        else:
            logger.info(f"ingest failed with status {response.status_code}: {response.text}")
            response.raise_for_status()
