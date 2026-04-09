import json
import pathlib

import pytest

from testing_common._infra import AgentSettings, Connection, DotEnvWriter, AppSettingsWriter
from testing_common._infra.config import _to_env_vars, _to_appsettings, _deep_merge


class TestToEnvVars:
    def test_standard_credentials(self):
        conn = Connection(client_id="cid", client_secret="sec", tenant_id="tid")
        result = _to_env_vars(conn)
        prefix = "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__"
        assert result[prefix + "CLIENTID"] == "cid"
        assert result[prefix + "CLIENTSECRET"] == "sec"
        assert result[prefix + "TENANTID"] == "tid"

    def test_federated_uses_federated_client_id(self):
        conn = Connection(federated_client_id="managed-id", tenant_id="tid")
        result = _to_env_vars(conn)
        prefix = "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__"
        assert result[prefix + "CLIENTID"] == "managed-id"
        assert result[prefix + "TENANTID"] == "tid"
        assert prefix + "CLIENTSECRET" not in result

    def test_omits_empty_standard_fields(self):
        conn = Connection(client_id="only-id")
        result = _to_env_vars(conn)
        prefix = "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__"
        assert prefix + "CLIENTID" in result
        assert prefix + "CLIENTSECRET" not in result
        assert prefix + "TENANTID" not in result


class TestToAppSettings:
    def test_standard_credentials_nested_structure(self):
        conn = Connection(client_id="cid", client_secret="sec", tenant_id="tid")
        result = _to_appsettings(conn)
        s = result["Connections"]["ServiceConnection"]["Settings"]
        assert s["ClientId"] == "cid"
        assert s["ClientSecret"] == "sec"
        assert s["TenantId"] == "tid"

    def test_federated_uses_federated_client_id(self):
        conn = Connection(federated_client_id="managed-id", tenant_id="tid")
        result = _to_appsettings(conn)
        s = result["Connections"]["ServiceConnection"]["Settings"]
        assert s["ClientId"] == "managed-id"
        assert s["TenantId"] == "tid"
        assert "ClientSecret" not in s

    def test_omits_empty_standard_fields(self):
        conn = Connection(client_id="only-id")
        result = _to_appsettings(conn)
        s = result["Connections"]["ServiceConnection"]["Settings"]
        assert "ClientId" in s
        assert "ClientSecret" not in s
        assert "TenantId" not in s


class TestDeepMerge:
    def test_adds_new_key(self):
        base = {"a": 1}
        _deep_merge(base, {"b": 2})
        assert base == {"a": 1, "b": 2}

    def test_overwrites_scalar(self):
        base = {"a": 1}
        _deep_merge(base, {"a": 99})
        assert base["a"] == 99

    def test_merges_nested_dicts(self):
        base = {"Connections": {"Other": "value"}}
        override = {"Connections": {"Service": "new"}}
        _deep_merge(base, override)
        assert base["Connections"]["Other"] == "value"
        assert base["Connections"]["Service"] == "new"

    def test_deep_nested_merge(self):
        base = {"A": {"B": {"C": 1, "D": 2}}}
        _deep_merge(base, {"A": {"B": {"C": 99}}})
        assert base["A"]["B"]["C"] == 99
        assert base["A"]["B"]["D"] == 2

    def test_scalar_replaced_by_dict(self):
        base = {"key": "scalar"}
        _deep_merge(base, {"key": {"nested": "val"}})
        assert base["key"] == {"nested": "val"}


class TestDotEnvWriter:
    def test_creates_new_file(self, tmp_path):
        env_file = tmp_path / ".env"
        conn = Connection(client_id="cid", client_secret="sec", tenant_id="tid")
        settings = AgentSettings().connection(conn)
        DotEnvWriter().write(settings, env_file)

        content = env_file.read_text()
        assert "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTID=cid" in content
        assert "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__CLIENTSECRET=sec" in content
        assert "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__TENANTID=tid" in content

    def test_merges_with_existing_vars(self, tmp_path):
        env_file = tmp_path / ".env"
        env_file.write_text("EXISTING_VAR=hello\nANOTHER=world\n")
        conn = Connection(client_id="cid", client_secret="sec", tenant_id="tid")
        settings = AgentSettings().connection(conn)
        DotEnvWriter().write(settings, env_file)

        content = env_file.read_text()
        assert "EXISTING_VAR=hello" in content
        assert "ANOTHER=world" in content
        assert "CLIENTID=cid" in content

    def test_overwrites_existing_connection_vars(self, tmp_path):
        prefix = "CONNECTIONS__SERVICE_CONNECTION__SETTINGS__"
        env_file = tmp_path / ".env"
        env_file.write_text(f"{prefix}CLIENTID=old_id\n")
        conn = Connection(client_id="new_id", tenant_id="tid")
        settings = AgentSettings().connection(conn)
        DotEnvWriter().write(settings, env_file)

        content = env_file.read_text()
        assert f"{prefix}CLIENTID=new_id" in content
        assert "old_id" not in content

    def test_ignores_comment_lines_in_existing_file(self, tmp_path):
        env_file = tmp_path / ".env"
        env_file.write_text("# This is a comment\nFOO=bar\n")
        settings = AgentSettings().connection(Connection(client_id="x"))
        DotEnvWriter().write(settings, env_file)

        content = env_file.read_text()
        assert "FOO=bar" in content
        # Comment is not carried through (not parsed as a key=value line)

    def test_no_connection_writes_empty_ish_file(self, tmp_path):
        env_file = tmp_path / ".env"
        env_file.write_text("KEEP=this\n")
        settings = AgentSettings()  # no connection set
        DotEnvWriter().write(settings, env_file)

        content = env_file.read_text()
        assert "KEEP=this" in content


class TestAppSettingsWriter:
    def test_creates_new_file(self, tmp_path):
        json_file = tmp_path / "appsettings.local.json"
        conn = Connection(client_id="cid", client_secret="sec", tenant_id="tid")
        settings = AgentSettings().connection(conn)
        AppSettingsWriter().write(settings, json_file)

        doc = json.loads(json_file.read_text())
        s = doc["Connections"]["ServiceConnection"]["Settings"]
        assert s["ClientId"] == "cid"
        assert s["ClientSecret"] == "sec"
        assert s["TenantId"] == "tid"

    def test_merges_with_existing_json(self, tmp_path):
        json_file = tmp_path / "appsettings.local.json"
        existing = {"Logging": {"LogLevel": {"Default": "Warning"}}}
        json_file.write_text(json.dumps(existing))
        conn = Connection(client_id="cid", client_secret="sec", tenant_id="tid")
        settings = AgentSettings().connection(conn)
        AppSettingsWriter().write(settings, json_file)

        doc = json.loads(json_file.read_text())
        assert doc["Logging"]["LogLevel"]["Default"] == "Warning"
        assert doc["Connections"]["ServiceConnection"]["Settings"]["ClientId"] == "cid"

    def test_overwrites_existing_connection_settings(self, tmp_path):
        json_file = tmp_path / "appsettings.local.json"
        existing = {
            "Connections": {
                "ServiceConnection": {"Settings": {"ClientId": "old", "ClientSecret": "old_s"}}
            }
        }
        json_file.write_text(json.dumps(existing))
        conn = Connection(client_id="new", client_secret="new_s", tenant_id="t")
        settings = AgentSettings().connection(conn)
        AppSettingsWriter().write(settings, json_file)

        doc = json.loads(json_file.read_text())
        s = doc["Connections"]["ServiceConnection"]["Settings"]
        assert s["ClientId"] == "new"
        assert s["ClientSecret"] == "new_s"

    def test_handles_corrupt_json_gracefully(self, tmp_path):
        json_file = tmp_path / "appsettings.local.json"
        json_file.write_text("{not valid json}")
        conn = Connection(client_id="cid", tenant_id="tid")
        settings = AgentSettings().connection(conn)
        AppSettingsWriter().write(settings, json_file)

        doc = json.loads(json_file.read_text())
        assert doc["Connections"]["ServiceConnection"]["Settings"]["ClientId"] == "cid"

    def test_output_is_valid_json(self, tmp_path):
        json_file = tmp_path / "appsettings.local.json"
        conn = Connection(client_id="cid", client_secret="sec", tenant_id="tid")
        settings = AgentSettings().connection(conn)
        AppSettingsWriter().write(settings, json_file)

        # Should not raise
        json.loads(json_file.read_text())

    def test_no_connection_writes_empty_doc(self, tmp_path):
        json_file = tmp_path / "appsettings.local.json"
        settings = AgentSettings()  # no connection
        AppSettingsWriter().write(settings, json_file)

        doc = json.loads(json_file.read_text())
        assert doc == {}
