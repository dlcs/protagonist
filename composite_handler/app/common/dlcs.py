import requests
from django.conf import settings


class DLCS:
    def __init__(self):
        self._api_root = settings.DLCS["api_root"].geturl()

    def test_credentials(self, customer, auth):
        response = requests.get(
            "{0}customers/{1}".format(self._api_root, customer),
            headers={"Content-Type": "application/json", "Authorization": auth},
        )
        if response.status_code == requests.codes.ok:
            return response.json()
        else:
            response.raise_for_status()

    def ingest(self, customer, json, auth):
        response = requests.post(
            "{0}customers/{1}/queue".format(self._api_root, customer),
            headers={"Content-Type": "application/json", "Authorization": auth},
            json=json,
        )
        if response.status_code == requests.codes.created:
            return response.json()
        else:
            response.raise_for_status()
