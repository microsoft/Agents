from microsoft_agents.hosting.core import CardFactory

def create_profile_card(profile):
    return CardFactory.adaptive_card(
        {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "version": "1.5",
            "type": "AdaptiveCard",
            "body": [
                {
                    "type": "ColumnSet",
                    "columns": [
                        {
                            "type": "Column",
                            "width": "auto",
                            "items": (
                                [
                                    {
                                        "type": "Image",
                                        "altText": "",
                                        "url": profile.get("imageUri", ""),
                                        "style": "Person",
                                        "size": "Small",
                                    }
                                ]
                                if profile.get("imageUri")
                                else []
                            ),
                        },
                        {
                            "type": "Column",
                            "width": "auto",
                            "items": [
                                {
                                    "type": "TextBlock",
                                    "weight": "Bolder",
                                    "text": profile["displayName"],
                                },
                                {
                                    "type": "Container",
                                    "spacing": "Small",
                                    "items": [
                                        {
                                            "type": "TextBlock",
                                            "text": profile["jobTitle"],
                                            "spacing": "Small",
                                        },
                                        {
                                            "type": "TextBlock",
                                            "text": profile["mail"],
                                            "spacing": "None",
                                        },
                                        {
                                            "type": "TextBlock",
                                            "text": profile["givenName"],
                                            "spacing": "None",
                                        },
                                        {
                                            "type": "TextBlock",
                                            "text": profile["surname"],
                                            "spacing": "None",
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                }
            ],
        }
    )


def create_pr_card(pr):
    return CardFactory.adaptive_card(
        {
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "type": "AdaptiveCard",
            "version": "1.0",
            "body": [
                {
                    "type": "TextBlock",
                    "text": pr.title,
                    "weight": "Bolder",
                    "size": "Medium",
                },
                {"type": "TextBlock", "text": pr.id},
            ],
            "actions": [
                {
                    "type": "Action.OpenUrl",
                    "title": "View Pull Request",
                    "url": pr.url,
                }
            ],
        }
    )
