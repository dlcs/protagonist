import json
import pathlib

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

with open(
    pathlib.Path(__file__).resolve().parent / "test_payloads/collection.json"
) as json_file:
    test_collection = json.load(json_file)

test_fixtures = {
    "collection": {"id": "c6683325-a036-4240-beab-74404bc80894", "response_code": 200},
    "members": {
        "PENDING": {
            "id": "3385e3b3-c6aa-4e26-b418-7a000e55d9f2",
            "created": "2021-12-02T09:53:40.702000Z",
            "last_updated": "2021-12-02T09:53:40.702000Z",
            "response_code": 200,
        },
        "FETCHING_ORIGIN": {
            "id": "3c331aa2-4f48-4abb-a8c8-374f57154271",
            "created": "2021-03-10T12:08:58.094000Z",
            "last_updated": "2021-06-25T11:07:27.492000Z",
            "response_code": 200,
        },
        "RASTERIZING": {
            "id": "5a926951-247a-4875-aca1-70ab2134eee9",
            "created": "2021-04-04T15:00:37.382000Z",
            "last_updated": "2021-10-27T21:31:29.037000Z",
            "response_code": 200,
        },
        "PUSHING_TO_DLCS": {
            "id": "87d0461a-d4a4-4159-bff4-548459bc5312",
            "created": "2021-04-04T12:36:35.626000Z",
            "last_updated": "2021-12-03T06:55:33.591000Z",
            "image_count": 783,
            "response_code": 200,
        },
        "BUILDING_DLCS_REQUEST": {
            "id": "86c8b2d7-7287-4d6a-9868-2267085eca6d",
            "created": "2021-01-26T12:11:32.594000Z",
            "last_updated": "2021-03-07T02:54:11.223000Z",
            "image_count": 633,
            "response_code": 200,
        },
        "COMPLETED": {
            "id": "fd5057bc-aef2-427f-8233-f9174e2f47a7",
            "created": "2021-10-06T13:46:43.400000Z",
            "last_updated": "2021-11-27T07:25:50.133000Z",
            "image_count": 875,
            "dlcs_uris": [
                "https://api.dlcs.digirati.io/customers/17/queue/batches/570756",
                "https://api.dlcs.digirati.io/customers/17/queue/batches/570757",
                "https://api.dlcs.digirati.io/customers/17/queue/batches/570758",
                "https://api.dlcs.digirati.io/customers/17/queue/batches/570759",
                "https://api.dlcs.digirati.io/customers/17/queue/batches/570760",
                "https://api.dlcs.digirati.io/customers/17/queue/batches/570761",
                "https://api.dlcs.digirati.io/customers/17/queue/batches/570762",
            ],
            "response_code": 200,
        },
        "ERROR": {
            "id": "1f1f0f9a-c0f2-420e-8ba8-8b445e3f98e2",
            "created": "2021-12-08T11:40:36.716000Z",
            "last_updated": "2021-11-12T10:55:06.686000Z",
            "error": "Exception: something bad happened",
            "response_code": 422,
        },
    },
}


@pytest.fixture(scope="session")
def http_service(docker_ip, docker_services):
    port = docker_services.port_for("api", 8000)
    url = f"http://{docker_ip}:{port}"
    docker_services.wait_until_responsive(
        timeout=300,
        pause=1,
        check=lambda: is_responsive(f"{url}/health-check/"),
    )
    return url


def test_collection_api_view(http_service):
    r = requests.post(
        f"{http_service}/customers/123/queue",
        headers=test_headers,
        json=test_collection,
    )
    assert r.status_code == 202

    act_vals = r.json()
    assert act_vals["id"]

    act_members = act_vals["members"]
    assert len(act_members) == 1
    for act_member in act_members:
        assert "id" in act_member
        assert act_member["status"] == "PENDING"
        assert "created" in act_member
        assert "last_updated" in act_member


def test_collection_query(http_service):
    collection_id = test_fixtures["collection"]["id"]

    r = requests.get(
        f"{http_service}/collections/{collection_id}",
        headers=test_headers,
    )
    assert r.status_code == 200

    act_vals = r.json()
    assert act_vals["id"] == build_uri(collection_id)

    act_members = act_vals["members"]
    assert len(act_members) == 7
    for act_member in act_members:
        verify_member(
            collection_id,
            act_member,
            act_member["status"],
            test_fixtures["members"].get(act_member["status"]),
        )


def test_member_query_pending(http_service):
    expected_status = "PENDING"
    validate_member(
        http_service, expected_status, test_fixtures["members"][expected_status]
    )


def test_member_query_fetching_origin(http_service):
    expected_status = "FETCHING_ORIGIN"
    validate_member(
        http_service, expected_status, test_fixtures["members"][expected_status]
    )


def test_member_query_rasterizing(http_service):
    expected_status = "RASTERIZING"
    validate_member(
        http_service, expected_status, test_fixtures["members"][expected_status]
    )


def test_member_query_pushing_to_dlcs(http_service):
    expected_status = "PUSHING_TO_DLCS"
    validate_member(
        http_service, expected_status, test_fixtures["members"][expected_status]
    )


def test_member_query_building_dlcs_request(http_service):
    expected_status = "BUILDING_DLCS_REQUEST"
    validate_member(
        http_service, expected_status, test_fixtures["members"][expected_status]
    )


def test_member_query_completed(http_service):
    expected_status = "COMPLETED"
    validate_member(
        http_service, expected_status, test_fixtures["members"][expected_status]
    )


def test_member_query_error(http_service):
    expected_status = "ERROR"
    validate_member(
        http_service, expected_status, test_fixtures["members"][expected_status]
    )


def validate_member(http_service, expected_status, expected_vals):
    collection_id = test_fixtures["collection"]["id"]

    r = requests.get(
        f"{http_service}/collections/{collection_id}/members/{expected_vals['id']}",
        headers=test_headers,
        allow_redirects=False,
    )
    assert r.status_code == expected_vals["response_code"]

    verify_member(collection_id, r.json(), expected_status, expected_vals)


def verify_member(collection_id, actual_member, expected_status, expected_member):
    assert actual_member["id"] == build_uri(collection_id, expected_member["id"])
    assert actual_member["status"] == expected_status
    assert actual_member["created"] == expected_member["created"]
    assert actual_member["last_updated"] == expected_member["last_updated"]

    if "image_count" in expected_member:
        assert actual_member["image_count"] == expected_member["image_count"]

    if "dlcs_uri" in expected_member:
        assert actual_member["dlcs_uri"] == expected_member["dlcs_uri"]

    if "error" in expected_member:
        assert actual_member["error"] == expected_member["error"]


def build_uri(collection_id, member_id=None):
    if member_id:
        return f"http://localhost:8000/collections/{collection_id}/members/{member_id}"
    else:
        return f"http://localhost:8000/collections/{collection_id}"
