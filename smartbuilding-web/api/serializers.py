from rest_framework import serializers


class LoginSerializer(serializers.Serializer):
    username = serializers.CharField(max_length=150)
    password = serializers.CharField(max_length=256)


class SyncEntityPayloadSerializer(serializers.Serializer):
    id = serializers.UUIDField()
    updatedAt = serializers.CharField()
    deletedAt = serializers.CharField(required=False, allow_blank=True, allow_null=True)
    jsonData = serializers.CharField()


class SyncPushRequestSerializer(serializers.Serializer):
    entityType = serializers.CharField(max_length=64)
    entities = SyncEntityPayloadSerializer(many=True)


class SyncPullQuerySerializer(serializers.Serializer):
    entityType = serializers.CharField(max_length=64)
    since = serializers.CharField()

