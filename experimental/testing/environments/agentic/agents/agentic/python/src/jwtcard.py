import json
import os


def load_adaptive_card(file_path):
    """Load Adaptive Card JSON from a file with validation."""
    if not os.path.exists(file_path):
        raise FileNotFoundError(f"Adaptive Card file not found: {file_path}")

    with open(file_path, "r", encoding="utf-8") as file:
        try:
            card_json = json.load(file)
        except json.JSONDecodeError as e:
            raise ValueError(f"Invalid JSON in {file_path}: {e}")

    return card_json


def update_card_data(card_json, data_map):
    """
    Replace placeholders in the Adaptive Card JSON with actual values.
    Placeholders should be in the form {{key}} in the JSON template.
    """
    card_str = json.dumps(card_json)  # Convert to string for replacement
    for key, value in data_map.items():
        placeholder = f"{{{{{key}}}}}"  # e.g., {{name}}
        card_str = card_str.replace(placeholder, str(value))

    return json.loads(card_str)  # Convert back to dict
