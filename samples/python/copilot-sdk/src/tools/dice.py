import random
import re
from pydantic import BaseModel, Field
from copilot import define_tool


class RollDiceParams(BaseModel):
    notation: str = Field(description="Dice notation like '2d6+3', '1d20', '4d8-1'")


@define_tool(description="Roll dice using standard notation (e.g., 2d6+3, 1d20)")
async def roll_dice(params: RollDiceParams) -> str:
    notation = params.notation.strip().lower()
    match = re.match(r"^(\d+)d(\d+)([+-]\d+)?$", notation)
    if not match:
        return f"Invalid dice notation: {params.notation}. Use format like 2d6+3"

    num_dice = int(match.group(1))
    num_sides = int(match.group(2))
    modifier = int(match.group(3)) if match.group(3) else 0

    if num_dice < 1 or num_dice > 100 or num_sides < 2 or num_sides > 1000:
        return "Please use reasonable dice values (1-100 dice, 2-1000 sides)"

    rolls = [random.randint(1, num_sides) for _ in range(num_dice)]
    total = sum(rolls) + modifier

    modifier_str = f" + {modifier}" if modifier > 0 else f" - {abs(modifier)}" if modifier < 0 else ""
    return f"🎲 Rolling {params.notation}: [{', '.join(str(r) for r in rolls)}]{modifier_str} = {total}"
