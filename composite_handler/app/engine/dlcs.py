import requests
from django.conf import settings


class DLCS:

    def __init__(self):
        self._api_root = settings.DLCS["api_root"].geturl()
        self._api_key = settings.DLCS["api_key"]
        self._api_secret = settings.DLCS["api_secret"]

    def ingest(self, customer, payload):
        response = requests.post(
            "{0}customers/{1}/queue".format(self._api_root, customer),
            auth=(self._api_key, self._api_secret),
            headers={"Content-Type": "application/json"},
            json=payload
        )
        if response.status_code == requests.codes.created:
            return response.json()
        else:
            response.raise_for_status()
