# Python Test Generation Overlay

This overlay extends the base `generate-tests` pattern with Python-specific guidance.

## Tools Available

Python does NOT have Aura semantic tools (no Roslyn equivalent). Use:

| Tool | Use For |
|------|---------|
| `mcp_pylance_mcp_s_pylanceDocuments` | Search Pylance docs |
| `mcp_pylance_mcp_s_pylanceInvokeRefactoring` | Limited refactoring |
| `run_in_terminal` | Run pytest |
| `create_file` / `replace_string_in_file` | Create/edit test files |

## Test Framework: pytest

```python
# tests/test_my_service.py
import pytest
from unittest.mock import Mock, patch
from my_service import MyService

class TestMyService:
    def test_method_when_condition_should_result(self):
        # Arrange
        sut = MyService()
        
        # Act
        result = sut.method()
        
        # Assert
        assert result == expected
```

## Mocking Patterns

### Mock Dependencies
```python
from unittest.mock import Mock, patch, MagicMock

# Mock a class
mock_client = Mock(spec=HttpClient)
mock_client.get.return_value = {"data": "value"}

# Patch a module-level function
with patch('my_module.external_call') as mock_call:
    mock_call.return_value = "mocked"
    result = function_under_test()
```

### Mock File System
```python
from unittest.mock import mock_open, patch

# Mock file reading
mock_content = "key: value"
with patch("builtins.open", mock_open(read_data=mock_content)):
    result = load_config("config.yaml")

# Or use pyfakefs
from pyfakefs.fake_filesystem_unittest import Patcher

with Patcher() as patcher:
    patcher.fs.create_file("/config.yaml", contents="key: value")
    result = load_config("/config.yaml")
```

## Fixtures

```python
import pytest

@pytest.fixture
def mock_client():
    client = Mock(spec=HttpClient)
    client.get.return_value = {"status": "ok"}
    return client

@pytest.fixture
def temp_config(tmp_path):
    config_file = tmp_path / "config.yaml"
    config_file.write_text("key: value")
    return config_file

def test_with_fixtures(mock_client, temp_config):
    sut = MyService(mock_client)
    result = sut.load(temp_config)
    assert result.key == "value"
```

## Workflow

1. **Create test file** (use `create_file`)
2. **Write test class and methods** (use `replace_string_in_file` to add tests)
3. **Run tests**:
   ```bash
   pytest tests/test_my_service.py -v
   ```
4. **Check coverage**:
   ```bash
   pytest tests/test_my_service.py --cov=my_service --cov-report=term-missing
   ```

## Test Naming Convention

```python
def test_<method>_when_<condition>_should_<result>(self):
    pass

# Examples:
def test_load_config_when_file_exists_should_return_config(self):
def test_load_config_when_file_missing_should_raise_error(self):
def test_process_when_empty_list_should_return_empty(self):
```

## Anti-patterns

❌ Don't use `aura_generate` for Python - it's C#/Roslyn only
❌ Don't forget `spec=` in `Mock()` - catches attribute errors
❌ Don't use `mock.patch` without context manager or decorator
❌ Don't hardcode paths - use `tmp_path` fixture
