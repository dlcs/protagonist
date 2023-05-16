import pathlib

import pytest


@pytest.fixture(scope="session")
def docker_compose_file(pytestconfig):
    return pathlib.Path(__file__).resolve().parent / "docker-compose.yml"
