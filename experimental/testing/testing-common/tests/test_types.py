from testing_common.types import SDKVersion


class TestSDKVersion:
    def test_values(self):
        assert SDKVersion.PYTHON.value == "python"
        assert SDKVersion.JS.value == "js"
        assert SDKVersion.NET.value == "net"

    def test_is_str_subclass(self):
        assert isinstance(SDKVersion.PYTHON, str)
        assert SDKVersion.PYTHON == "python"

    def test_members(self):
        assert set(SDKVersion) == {SDKVersion.PYTHON, SDKVersion.JS, SDKVersion.NET}
