from microsoft_agents.activity import ActionTypes, Activity, ActivityTypes, Attachment
from microsoft_agents.hosting.core import CardFactory, TurnContext

from microsoft_agents.activity import (
    HeroCard,
    AnimationCard,
    AudioCard,
    ReceiptCard,
    ReceiptItem,
    ThumbnailCard,
    VideoCard,
    CardAction,
    CardImage,
    MediaUrl,
    ThumbnailUrl,
    Fact,
)


class CardMessages:

    @staticmethod
    async def send_intro_card(context: TurnContext):
        buttons = [
            CardAction(type=ActionTypes.im_back, value="1", title="Adaptive Card"),
            CardAction(type=ActionTypes.im_back, value="2", title="Animation Card"),
            CardAction(type=ActionTypes.im_back, value="3", title="Audio Card"),
            CardAction(type=ActionTypes.im_back, value="4", title="Hero Card"),
            CardAction(type=ActionTypes.im_back, value="5", title="Receipt Card"),
            CardAction(type=ActionTypes.im_back, value="6", title="Thumbnail Card"),
            CardAction(type=ActionTypes.im_back, value="7", title="Video Card"),
        ]
        card = CardFactory.hero_card(
            HeroCard(
                title="Cards",
                buttons=buttons,
                text="Select one of the following choices",
            )
        )
        await CardMessages.send_activity(context, card)

    @staticmethod
    async def send_adaptive_card(context: TurnContext, adaptive_card: dict):
        card = CardFactory.adaptive_card(adaptive_card)
        await CardMessages.send_activity(context, card)

    @staticmethod
    async def send_animation_card(context: TurnContext):
        card = CardFactory.animation_card(
            AnimationCard(
                title="Microsoft Agents Framework",
                image=ThumbnailUrl(
                    url="https://i.giphy.com/Ki55RUbOV5njy.gif", alt="Cute Robot"
                ),
                media=[MediaUrl(url="https://i.giphy.com/Ki55RUbOV5njy.gif")],
                subtitle="Animation Card",
                text="This is an example of an animation card using a gif.",
                aspect="16:9",
                duration="PT2M",
            )
        )
        await CardMessages.send_activity(context, card)

    @staticmethod
    async def send_audio_card(context: TurnContext):
        card = CardFactory.audio_card(
            AudioCard(
                title="I am your father",
                media=[
                    MediaUrl(
                        url="https://www.mediacollege.com/downloads/sound-effects/star-wars/darthvader/darthvader_yourfather.wav",
                        profile="Darth Vader - I am your father",
                    )
                ],
                buttons=[
                    CardAction(
                        type=ActionTypes.open_url,
                        title="Read more",
                        value="https://en.wikipedia.org/wiki/The_Empire_Strikes_Back",
                    )
                ],
                subtitle="Star Wars: Episode V - The Empire Strikes Back",
                text="The Empire Strikes Back (also known as Star Wars: Episode V - The Empire Strikes Back) is a 1980 American epic space opera film directed by Irvin Kershner. Leigh Brackett and Lawrence Kasdan wrote the screenplay, with George Lucas writing the film's story and serving as executive producer. The second installment in the original Star Wars trilogy, it was produced by Gary Kurtz for Lucasfilm Ltd. and stars Mark Hamill, Harrison Ford, Carrie Fisher, Billy Dee Williams, Anthony Daniels, David Prowse, Kenny Baker, Peter Mayhew and Frank Oz.",
                image=ThumbnailUrl(
                    url="https://upload.wikimedia.org/wikipedia/en/3/3c/SW_-_Empire_Strikes_Back.jpg",
                    alt="The Empire Strikes Back",
                ),
                aspect="16:9",
                duration="PT2M",
            )
        )
        await CardMessages.send_activity(context, card)

    @staticmethod
    async def send_hero_card(context):
        card = CardFactory.hero_card(
            HeroCard(
                title="Copilot Hero Card",
                images=[
                    CardImage(
                        url="https://blogs.microsoft.com/wp-content/uploads/prod/2023/09/Press-Image_FINAL_16x9-4.jpg"
                    )
                ],
                buttons=[
                    CardAction(
                        type=ActionTypes.open_url,
                        title="Get started",
                        value="https://docs.microsoft.com/en-us/azure/bot-service/",
                    )
                ],
            )
        )
        await CardMessages.send_activity(context, card)

    @staticmethod
    async def send_receipt_card(context):
        card = CardFactory.receipt_card(
            ReceiptCard(
                title="John Doe",
                facts=[
                    Fact(key="Order Number", value="1234"),
                    Fact(key="Payment Method", value="VISA 5555-****"),
                ],
                items=[
                    ReceiptItem(
                        title="Data Transfer",
                        price="$38.45",
                        quantity="368",
                        image=CardImage(
                            url="https://github.com/amido/azure-vector-icons/raw/master/renders/traffic-manager.png"
                        ),
                    ),
                    ReceiptItem(
                        title="App Service",
                        price="$45.00",
                        quantity="720",
                        image=CardImage(
                            url="https://github.com/amido/azure-vector-icons/raw/master/renders/cloud-service.png"
                        ),
                    ),
                ],
                tax="$7.50",
                total="$90.95",
                buttons=[
                    CardAction(
                        type=ActionTypes.open_url,
                        title="More information",
                        value="https://azure.microsoft.com/en-us/pricing/details/bot-service/",
                    ),
                ],
            )
        )
        await CardMessages.send_activity(context, card)

    @staticmethod
    async def send_thumbnail_card(context):
        card = CardFactory.thumbnail_card(
            ThumbnailCard(
                title="Copilot Thumbnail Card",
                images=[
                    CardImage(
                        url="https://blogs.microsoft.com/wp-content/uploads/prod/2023/09/Press-Image_FINAL_16x9-4.jpg"
                    )
                ],
                buttons=[
                    CardAction(
                        type=ActionTypes.open_url,
                        title="Get started",
                        value="https://docs.microsoft.com/en-us/azure/bot-service/",
                    )
                ],
                subtitle="Your bots - wherever your users are talking",
                text="Build and connect intelligent bots to interact with your users naturally wherever they are, from text/sms to Skype, Slack, Office 365 mail and other popular services.",
            )
        )
        await CardMessages.send_activity(context, card)

    @staticmethod
    async def send_video_card(context):
        card = CardFactory.video_card(
            VideoCard(
                title="M365 Copilot",
                media=[MediaUrl(url="https://youtu.be/zqH-HtQbaeU")],
                buttons=[
                    CardAction(
                        type=ActionTypes.open_url,
                        title="Learn More",
                        value="https://youtu.be/zqH-HtQbaeU",
                    )
                ],
                subtitle="by Microsoft Helps",
                text="Copilot is a new way to interact with your data and applications using natural language. It is designed to help you get things done faster and more efficiently.",
            )
        )
        await CardMessages.send_activity(context, card)

    @staticmethod
    async def send_activity(context: TurnContext, card: Attachment):
        activity = Activity(type=ActivityTypes.message, attachments=[card])
        await context.send_activity(activity)
