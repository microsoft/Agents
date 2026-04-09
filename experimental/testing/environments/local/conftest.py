# environments/local/conftest.py
#
# Local environment: agents run as subprocesses via run_agent.ps1.
# The scenario fixture (parametrized over all languages) will live here once
# the microsoft-agents-testing library fixture refactor is complete (see PLAN.md).
# Until then, test files declare scenarios directly via @pytest.mark.agent_test.
