import json

import pytest
import requests
from requests.exceptions import ConnectionError


def is_responsive(url):
    try:
        response = requests.get(url)
        if response.status_code == 200:
            return True
    except ConnectionError:
        return False


test_headers = {
    "Content-Type": "application/json",
    "Accept": "application/json",
    "Authorization": "ABC123",
}

with open("tests/api/test_data/collection.json") as json_file:
    test_collection = json.load(json_file)


@pytest.fixture(scope="session")
def http_service(docker_ip, docker_services):
    port = docker_services.port_for("api", 8000)
    url = f"http://{docker_ip}:{port}"
    docker_services.wait_until_responsive(
        timeout=300,
        pause=1,
        check=lambda: is_responsive("{}/health-check/".format(url)),
    )
    return url


def test_collection_api_view(http_service):
    response = requests.post(
        f"{http_service}/customers/123/queue",
        headers=test_headers,
        json=test_collection,
    )
    assert response.status_code == 202
    json_collection = response.json()
    assert json_collection["id"]
    json_members = json_collection["members"]
    assert len(json_members) == 6
    for json_member in json_members:
        assert json_member["id"]
        assert json_member["status"] == "PENDING"
        assert json_member["last_updated"]
