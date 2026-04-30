import pytest

from testing_common._infra import AgentSettings, Connection


class TestConnection:
    def test_defaults_are_empty_strings(self):
        conn = Connection()
        assert conn.client_id == ""
        assert conn.client_secret == ""
        assert conn.tenant_id == ""
        assert conn.federated_client_id == ""

    def test_is_federated_false_when_no_federated_id(self):
        conn = Connection(client_id="abc", client_secret="secret", tenant_id="tenant")
        assert conn.is_federated() is False

    def test_is_federated_true_when_federated_id_set(self):
        conn = Connection(federated_client_id="managed-id", tenant_id="tenant")
        assert conn.is_federated() is True

    def test_is_federated_false_for_empty_string(self):
        conn = Connection(federated_client_id="")
        assert conn.is_federated() is False

    def test_fields_stored_correctly(self):
        conn = Connection(
            client_id="id1",
            client_secret="s3cr3t",
            tenant_id="t1",
            federated_client_id="",
        )
        assert conn.client_id == "id1"
        assert conn.client_secret == "s3cr3t"
        assert conn.tenant_id == "t1"


class TestAgentSettings:
    def test_conn_is_none_by_default(self):
        settings = AgentSettings()
        assert settings.conn is None

    def test_fluent_connection_builder_returns_self(self):
        settings = AgentSettings()
        conn = Connection(client_id="a", client_secret="b", tenant_id="c")
        result = settings.connection(conn)
        assert result is settings

    def test_conn_property_returns_set_connection(self):
        conn = Connection(client_id="a", client_secret="b", tenant_id="c")
        settings = AgentSettings().connection(conn)
        assert settings.conn is conn

    def test_connection_can_be_overwritten(self):
        conn1 = Connection(client_id="first")
        conn2 = Connection(client_id="second")
        settings = AgentSettings().connection(conn1).connection(conn2)
        assert settings.conn is conn2

    def test_chaining_multiple_calls(self):
        conn = Connection(client_id="x", tenant_id="y")
        settings = AgentSettings().connection(conn)
        assert settings.conn.client_id == "x"
        assert settings.conn.tenant_id == "y"
