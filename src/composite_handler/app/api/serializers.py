import json
import pathlib

from app.common.models import Member, Collection
from jsonschema import ValidationError, RefResolver, Draft7Validator
from rest_framework import serializers


def __initialise_json_schemas():
    schema_dir = pathlib.Path(__file__).resolve().parent / "schemas"

    with open(schema_dir / "collection.schema.json", "r") as collection_schema_file:
        collection_schema = json.load(collection_schema_file)

    with open(schema_dir / "member.schema.json", "r") as member_schema_file:
        member_schema = json.load(member_schema_file)

    resolver = RefResolver.from_schema(
        collection_schema,
        store={
            collection_schema["$id"]: collection_schema,
            member_schema["$id"]: member_schema,
        },
    )

    return collection_schema, member_schema, resolver


_collection_schema, _member_schema, _resolver = __initialise_json_schemas()


class MemberSerializer(serializers.ModelSerializer):
    def validate_json_data(self, json_data):
        return _validate_json(json_data, _member_schema)

    class Meta:
        model = Member
        fields = "__all__"


class CollectionSerializer(serializers.ModelSerializer):
    def validate_json_data(self, json_data):
        return _validate_json(json_data, _collection_schema)

    class Meta:
        model = Collection
        fields = "__all__"


def _validate_json(json_data, json_schema):
    try:
        validator = Draft7Validator(json_schema, resolver=_resolver)
        validator.validate(json_data, json_schema)
    except ValidationError as err:
        raise serializers.ValidationError(err.message)
    return json_data
