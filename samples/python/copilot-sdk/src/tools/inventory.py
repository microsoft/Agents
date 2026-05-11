from typing import Optional
from pydantic import BaseModel, Field
from copilot import define_tool

_inventories: dict[str, list[str]] = {}


class ManageInventoryParams(BaseModel):
    action: str = Field(description="Action to perform: 'add', 'remove', or 'list'")
    item: Optional[str] = Field(default=None, description="Item name (required for add/remove)")


def create_inventory_tool(conversation_id: str):
    """Create an inventory tool scoped to a specific conversation."""

    @define_tool(description="Manage the adventurer's inventory - add, remove, or list items")
    async def manage_inventory(params: ManageInventoryParams) -> str:
        inventory = _inventories.setdefault(conversation_id, [])

        if params.action == "add":
            if not params.item:
                return "Specify an item to add to the inventory"
            inventory.append(params.item)
            return f"📦 Added '{params.item}' to inventory. ({len(inventory)} items total)"

        elif params.action == "remove":
            if not params.item:
                return "Specify an item to remove from the inventory"
            if params.item in inventory:
                inventory.remove(params.item)
                return f"🗑️ Removed '{params.item}' from inventory. ({len(inventory)} items remaining)"
            return f"'{params.item}' not found in inventory"

        elif params.action == "list":
            if not inventory:
                return "🎒 The adventurer's pack is empty"
            items_list = "\n".join(f"  • {item}" for item in inventory)
            return f"🎒 Inventory ({len(inventory)} items):\n{items_list}"

        return f"Unknown action '{params.action}'. Use 'add', 'remove', or 'list'"

    return manage_inventory
