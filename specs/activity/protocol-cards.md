# Activity Protocol -- Cards

Version: Provisional 3.2

## Abstract

The Activity Protocol Cards schema is an application-level representation of interactive cards for use within chat and other applications. The schema includes provisions for controlling the content, layout, and interactive elements of a card.

This schema is used within the Activity Protocol schema and is implemented by Microsoft chat systems and by interoperable bots and clients from many sources.

## Table of Contents

1. [Introduction](#Introduction)
2. [Card structure](#Card-structure)
3. [Basic cards](#Basic-cards)
4. [Media cards](#Media-cards)
5. [Animation card](#Animation-card)
6. [Audio card](#Audio-card)
7. [Video card](#Video-card)
8. [Receipt card](#Receipt-card)
9. [Signin card](#Signin-card)
10. [Complex types](#Complex-types)
11. [References](#References)

## Introduction

### Overview

The Activity Protocol Card schema provides a mechanism for transmitting content, layout information, and interactive elements within an interactive card. This specification does not describe the final visual form of a card, but the structure of the card suggests a layout without prescribing particular visual styles.

These cards are typically presented as attachments within the [Activity Protocol Schema](../ManifestDefinition/BotFramework-Activity.md) schema.

Cards are each oriented around a particular kind of content (e.g. thumbnail images, transaction receipts) but use a shared set of features and conventions. These cards are designed to be rendered within interfaces that do not have full fidelity or interactive elements; in these cases, this specification provides guidance on how to predictably and gracefully reduce the complexity of each card to preserve the intent of the original item.

### Relationship with Adaptive Cards

The Card schema was developed in conjunction with the v3 Activity Protocol. After Activity Protocol v3 was released, a new effort to supplant Cards with cross-application, generic cards resulted in the [Adaptive Card](https://adaptivecards.io) [[2](#References)] schema. Adaptive Cards provide a content-neutral format for data layout, in contrast to the content-specific Activity Protocol Cards.

Adaptive Cards are the recommended choice for new card content where supported. This is true even when a specific Activity Protocol Card exists.

Activity Protocol Cards are available where Adaptive Cards are not supported, and will be supported for existing users of the format until further announcements are made.

### Requirements

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED", "MAY", and "OPTIONAL" in this document are to be interpreted as described in [RFC 2119](https://tools.ietf.org/html/rfc2119) [[3](#References)].

An implementation is not compliant if it fails to satisfy one or more of the MUST or REQUIRED level requirements for the protocols it implements. An implementation that satisfies all the MUST or REQUIRED level and all the SHOULD level requirements for its protocols is said to be "unconditionally compliant"; one that satisfies all the MUST level requirements but not all the SHOULD level requirements for its protocols is said to be "conditionally compliant."

### Numbered requirements

Lines beginning with markers of the form `CXXXX` are specific requirements designed to be referenced by number in discussion outside of this document. They do not carry any more or less weight than normative statements made outside of `CXXXX` lines.

`C1000`: Editors of this specification MAY add new `CXXXX` requirements. They SHOULD find numeric `CXXXX` values that preserve the document's flow.

`C1001`: Editors MUST NOT renumber existing `CXXXX` requirements.

`C1002`: Editors MAY delete or revise `CXXXX` requirements. If revised, editors SHOULD retain the existing `CXXXX` value if the topic of the requirement remains largely intact.

`C1003`: Editors SHOULD NOT reuse retired `CXXXX` values. A list of deleted values MAY be maintained at the end of this document.

### Terminology

activity
> An action expressed by a bot, a channel, or a client that conforms to the Activity schema.

card
> A Activity Protocol Card that adheres to this specification

channel
> Software that sends and receives activities, and transforms them to and from chat or application behaviors. Channels are the authoritative store for activity data.

bot
> Software that sends and receives activities, and generates automated, semi-automated, or entirely manual responses. Bots have endpoints that are registered with channels.

client
> Software that sends and receives activities, typically on behalf of human users. Clients do not have endpoints.

sender
> Software transmitting an activity.

receiver
> Software accepting an activity.

field
> A named value within an activity or nested object.

## Card structure

This section defines the requirements for the basic structure of each card.

Cards include a flat list of name/value pairs, called fields. Fields may be primitive types. JSON is used as the common interchange format and although not all cards must be serialized to JSON at all times, they must be serializable to it. This allows implementations to rely on a simple set of conventions for handling known and unknown activity fields.

`C2001`: Cards MUST be serializable to the JSON format, including adherence to e.g. field uniqueness constraints.

`C2002`: Receivers MAY allow improperly-cased field names, although this is not required. Receivers MAY reject cards that do not include fields with the proper casing.

`C2003`: Receivers MAY reject cards that contain field values whose types do not match the value types described in this specification.

`C2004`: Unless otherwise noted, senders SHOULD NOT include empty string values for string fields.

`C2005`: Unless otherwise noted, senders MAY include additional fields within the card or any nested complex objects. Receivers MUST accept fields they do not understand.

### Content-type

Cards are identified by a MIME-compatible [[4](#References)] content-type. These content-types are not specified within the card itself; instead, these values accompany the card payload. In the [Activity Protocol Schema](./protocol-activity.md) [[1](#References)] schema, cards are included within [attachments](./protocol-activity.md#attachment) alongside the corresponding [`contentType`](./protocol-activity.md#content-type) field. 

### Transformations and display

Displaying a card within a client user interface frequently requires the implementer to make specific decisions about how to order and constrain content to match the visual style of the enclosing design. Further, this specification anticipates that these limitations are largely unavoidable and aims to assist implementers in choosing a path that makes graceful degradation of functionality possible.

This section provides guidance on which transformations best achieve the goal of preserving the intent of the card.

`C3000`: A Channel's card transformations and display MUST account for all known Activity Protocol Card types and include best-effort transformations into formats suitable for display to users.

`C3002`: Channels SHOULD preserve the ordering of text-bearing content fields as presented within this specification. Channels SHOULD NOT change the displayed format of the card based on how the fields appear in a payload transmitted by a sender.

`C3003`: Channels MAY alter the position of non-text multimedia content within the card in relation to text-bearing fields in order to achieve visual layout goals. Channels SHOULD preserve the order of non-text multimedia content as presented within the specification.

The `C3003` requirement is intended to allow repositioning of e.g. an image below or above a card title to match visual style within the channel.

`C3004`: Channels SHOULD preserve all content within the card.

`C3005`: Channels MAY establish rules requiring specific fields within card types.

Channels are recommended to make these rules simple.

`C3006`: Unless otherwise noted in this specification or in accordance with `C3004`, all card fields are optional.

### Buttons

All cards include content (in the form of one or more fields) and an array of buttons, each of which is represented by an action. Some cards include an additional card action, the "tap action," describing the behavior when the user taps the card but not any of the buttons.

All Activity Protocol Card types except Adaptive Cards include a `buttons` field, which contains zero or more buttons to be presented to a user. The type of the `buttons` field is an array of type [`cardAction`](./protocol-activity.md#card-action), defined in the [Activity Protocol Schema](./protocol-activity.md) schema [[1](#References)].

`C4000`: Channels MAY define a minimum and maximum number of buttons to allow on each card type.

Channels are recommended to use simple values for the minimum and maximum number of buttons and avoid complex formulas, such as changing the total number of buttons based on whether certain kinds of content are used.

`C4001`: Channels MUST NOT change the order of buttons on a card.

`C4002`: If a bot sends a button with action type not supported by the channel, the channel SHOULD preserve the button but provide a degraded experience.

The best degraded experience is an interactive dialog that, upon being clicked, tells the user that the bot sent an action that is not supported. If this is not possible, showing a disabled button is preferred. If this is not possible, the button should be dropped, taking care that the order of other buttons not be changed per `C4001`.

`C4004`: If a bot sends fewer than the minimum or more than the maximum number of buttons allowed on a card, the channel MAY drop the card. Alternatively, the channel MAY down-render the card using rules described for that card type.

## Basic cards

Activity Protocol defines two cards for presenting a mix of image, text, and interactive elements:
* [Hero cards](#Hero-cards), which present included images in a large banner
* [Thumbnail cards](#Thumbnail-cards), which present included images as thumbnails

Both basic cards support a shared set of properties and generally have card-specific layout.

#### Basic card title

The `title` field contains the title of the card. The value of the `title` field is of type string.

#### Basic card subtitle

The `subtitle` field contains the subtitle of the card. The value of the `subtitle` field is of type string.

#### Basic card text

The `text` field contains the text of the card. The value of the `text` field is of type string.

#### Basic card images

The `images` field contains one or more images to be shown within the card. The value of the `images` field is an array of type [`cardImage`](#Card-image).

`C5400`: If a bot sends more than the maximum number of images allowed on a card, the channel SHOULD drop excess images.

`C5401`: If a bot sends fewer than the minimum or more than the maximum number of images allowed on a card, the channel MAY drop the card.

#### Basic card buttons

The `buttons` field is described in detail in the above section titled [Buttons](#Buttons).

#### Basic card tap

The `tap` field contains an action that may be activated when a user clicks on a non-button or otherwise non-interactive part of a card. The value of the `tap` field is of type [`cardAction`](#Card-action).

`C5500`: Channels that do not support interactive image actions SHOULD drop the `tap` field while preserving the containing card.

## Media cards

Activity Protocol defines three kinds of cards designed expressly to transmit rich media content:
* [Animation cards](#Animation-card), for animation or video typically without sound.
* [Audio cards](#Audio-card), for audio without video
* [Video cards](#Video-card), for video that may or may not contain audio

All media cards support a shared set of properties and have card-specific behaviors.

`C6000`: Channels that can display media card content SHOULD document which formats are supported and corresponding limitations (minimum/maximum bitrate, resolution, etc.).

`C6001`: Channels SHOULD use HTTP content-type when retrieving the media to establish its actual type.

`C6002`: A channel that receives media card content of an unsupported type MAY send a hyperlink to the media to the user.

#### Media card title

The `title` field contains the title of the card. The value of the `title` field is of type string.

#### Media card subtitle

The `subtitle` field contains the subtitle of the card. The value of the `subtitle` field is of type string.

#### Media card image

The `image` field contains a placeholder image to be used in place of the media. The value of the `image` field is a complex object of type [`thumbnailUrl`](#Thumbnail-URL).

#### Media card media

The `media` field contains one or more alternative formats of the same media to be presented. The value of the `media` field is an array of complex objects of type ['mediaUrl'](#Media-URL).

`C6401`: If a receiver receives multiple objects within the `media` array, it MAY prefer media whose `profile` field it understands.

`C6402`: If a receiver receives multiple objects within the `media` array and it does not understand the `profile` field of any, it SHOULD display the first object in the array.

#### Media card buttons

The `buttons` field is described in detail in the above section titled [Buttons](#Buttons).

`C6500`: Senders SHOULD NOT include media transport control buttons (e.g., play/pause). These are intrinsic to the media playback window.

#### Media card shareable

The `shareable` field describes whether a client user experience should allow the user to share the content. The value of the `shareable` field is a boolean. If omitted, the default value is `true`.

`C6600`: Channels that do not have configurable settings for sharing content SHOULD ignore the value of the `shareable` field.

#### Media card autoloop

The `autoloop` field describes whether a client should automatically restart the media when it reaches the end of its content. The value of the `autoloop` field is a boolean. If omitted, the default value is `true`.

`C6700`: Channels that do not have configurable looping for content SHOULD ignore the value of the `autoloop` field.

#### Media card autostart

The `autostart` field describes whether a client should automatically start playing content when received. The value of the `autostart` field is a boolean. If omitted, the default value is `true`.

`C6800`: Channels SHOULD honor the value of the `autostart` if able and if doing so does not degrade a user's experience.

#### Media card aspect

The `aspect` field describes the visual aspect ratio of the media content. The value of the `aspect` field is a string with allowed values of `16:9` and `4:3`. `16:9` corresponds to media with width-to-height ratio of 16 to 9; `4:3` corresponds to media with width-to-height ratio of 4 to 3. Any other values have undefined meaning.

This field is advisory in nature only. The media's actual aspect ratio should be determined by inspecting the media itself.

`C6900`: Senders SHOULD list an `aspect` value corresponding to the media linked in the [`media`](#Media-card-media) field.

`C6901`: If media of multiple aspect ratios is included in the [`media`](#Media-card-media) field, senders SHOULD include an aspect ratio for the first object in the array.

`C6902`: Senders MAY include `aspect` values for media that do not have visual aspect ratios (e.g., audio-only media) if the `aspect` field applies to the [`image`](#Media-card-image) field for the card.

`C6903`: Senders SHOULD NOT include `aspect` values if the media does not have a visual aspect ration and if no [`image`](#Media-card-image) field is supplied.

`C6904`: Senders SHOULD NOT include values other than `16:9` or `4:3` unless it has knowledge that the receiver supports it.

#### Media card duration

The `duration` field describes the length of the media content without requiring a receiver to open the content. The value of the `duration` field is a string in [ISO 8601 duration format]((https://www.iso.org/iso-8601-date-and-time-format.html) [[6](#References)]).

`C6920`: Senders SHOULD only send values in the ISO 8601 Duration format. Senders SHOULD NOT send times or time intervals in the `duration` field.

`C6930`: Receivers SHOULD ignore `duration` fields they do not understand or which are in conflict with length encoded into the media.

#### Media card value

The `value` field contains a programmatic payload specific to this media card.

`C6950`: Senders SHOULD NOT include `value` fields of primitive types (e.g. string, int). `value` fields SHOULD be complex types or omitted.

### Animation card

Animation cards contain animated image content. Typically this content does not contain sound, and is typically presented with minimal transport controls (e.g, pause/play) or no transport controls at all. Some channels treat animation and video content the same way.

Animation cards follow all shared rules defined for [Media cards](#Media-cards).

Animation cards are identified by a `contentType` value of `application/vnd.microsoft.card.animation`.

### Audio card

Audio cards contain audio content.

Audio cards follow all shared rules defined for [Media cards](#Media-cards).

Audio cards are identified by a `contentType` value of `application/vnd.microsoft.card.audio`.

`C7100`: Senders SHOULD NOT send video content within audio cards.

`C7101`: If it receives video content within an audio card, a client MAY elect to present only the audio portion of the media.

### Video card

Video cards contain video content. Typically this content is presented to the user with advanced transport controls (e.g. rewind/restart/pause/play). Some channels treat animation and video content the same way.

Video cards follow all shared rules defined for [Media cards](#Media-cards).

Video cards are identified by a `contentType` value of `application/vnd.microsoft.card.video`.

## Receipt card

Receipt cards contain two tables of data (a list of receipt items, and a list of facts). They are intended to be an informative display and are not necessarily actionable on their own. For integration with payment systems suitable for conducting transactions, see the [payment action](#Payment).

Receipt cards are identified by a `contentType` value of `application/vnd.microsoft.card.receipt`.

`C7300`: Channels SHOULD render the `items` and `facts` fields as visual tables or approximations of tables (using e.g. fixed-width formatting).

`C7301`: Channels SHOULD render a delimiter between the `items` and `facts` field if both are present and contain any elements.

### Receipt card title

The `title` field contains the title of the card. The value of the `title` field is of type string.

### Receipt card items

The `items` field contains receipt items to be displayed in tabular form. The value of the `items` field is an array of type [`receiptItem`](#Receipt-item).

`C7310`: Channels MUST NOT alter the order of items within the `items` array.

### Receipt card facts

The `facts` field contains a flat list of key/value pairs to be displayed in tabular form. The value of the `facts` field is an array of type [`fact`](#Fact).

`C7320`: Channels MUST NOT alter the order of items within the `facts` array.

`C7321`: Channels SHOULD NOT reject the `facts` field or its contents if more than two items have the same [`key`](#Fact-key).

Although `facts` contains key-value pairs, receivers should not take the key-value pairing literally, and should allow duplicates per `C7531`.

### Receipt card tap

The `tap` field contains an action that may be activated when a user clicks on a non-button or otherwise non-interactive part of a card. The value of the `tap` field is of type [`cardAction`](#Card-action).

`C7330`: Channels that do not support interactive image actions SHOULD drop the `tap` field while preserving the containing card.

### Receipt card total

The `total` field contains the total field to be displayed on the receipt. The value of the `total` field is a string.

### Receipt card tax

The `tax` field contains the tax field to be displayed on the receipt. The value of the `tax` field is a string.

### Receipt card VAT

The `vat` field contains the value-added tax (VAT) field to be displayed on the receipt. The value of the `vat` field is a string.

### Receipt card buttons

The `buttons` field is described in detail in the above section titled [Buttons](#Buttons).

## Signin card

Signin cards are used to send a sign-in request to a user. Channels that support sign-in cards typically adorn the card with additional visual style to designate it as a sign-in card.

Signin cards are identified by a `contentType` value of `application/vnd.microsoft.card.signin`.

### Signin card text

The `text` field contains the text of the card. The value of the `text` field is of type string.

### Signin card buttons

The `buttons` field is described in detail in the above section titled [Buttons](#Buttons).

## Complex types

This section defines complex types used within the activity schema, described above.

### Thumbnail URL

Some clients have the ability to display custom thumbnails for non-interactive attachments or as placeholders for interactive attachments. The `thumbnailUrl` field identifies the source for this thumbnail. Data URIs, as defined in [RFC 2397](https://tools.ietf.org/html/rfc2397) [[5](#References)] are typically also allowed.

`C8000`: Receivers SHOULD accept HTTPS URLs.

`C8001`: Receivers MAY accept HTTP URLs.

`C8002`: Channels SHOULD accept data URIs.

`C8003`: Channels SHOULD NOT send `thumbnailUrl` fields to bots.

### Card image

Card images are used to display image content within [Hero cards](#Hero-cards) and [Thumbnail cards](#Thumbnail-cards).

#### Card image URL

The `url` field references image content to be displayed within a card. Data URIs, as defined in [RFC 2397](https://tools.ietf.org/html/rfc2397) [[5](#References)] are typically supported by channels. The value of the `url` field is of type string.

`C8100`: Channels SHOULD accept HTTPS URLs.

`C8101`: Channels MAY accept HTTP URLs.

`C8102`: Channels SHOULD accept data URIs.

#### Card image Alt tag

The `alt` field contains equivalent content for clients that cannot process images or have not yet loaded the image. The value of the `alt` field is a string.

#### Card image tap

The `tap` field contains an action to be activated if the user taps on an image or associated framing. The value of the `tap` field is of type [`cardAction`](#Card-action).

`C8120`: Channels that do not support interactive image actions SHOULD drop the `tap` field while preserving the containing card image.

### Media URL

A media URL object contains information about a media attachment.

#### Media URL URL

The `url` field contains a URL to the media. Because media URL objects do not contain content type information, data URIs, as defined in [RFC 2397](https://tools.ietf.org/html/rfc2397) [5](#References),  are not supported.

`C8200`: Receivers SHOULD accept HTTPS URLs.

`C8201`: Receivers MAY accept HTTP URLs.

`C8202`: Senders SHOULD NOT send data URIs.

#### Media URL profile

The `profile` field contains an optional hint to the client to allow it to differentiate between media URLs. The value of the `profile` field is of type string and is defined by the channel.

### Receipt item

A `receiptItem` is a single row to be shown within the [`items`](#Receipt-card-items) field of a [receipt card](#Receipt-card).

#### Receipt item title

The `title` field contains the title for the receipt item. The value of the `title` field is a string.

#### Receipt item subtitle

The `subtitle` field contains the subtitle for the receipt item. The value of the `subtitle` field is a string.

#### Receipt item text

The `text` field contains the text for the receipt item. The value of the `text` field is a string.

#### Receipt item image

The `image` field contains an image to be shown within the receipt item row. The value of the `image` field is an object of type [`cardImage`](#Card-image).

#### Receipt item price

The `price` field contains the displayed unit price with accompanying currency symbols. The value of the `price` field is a string.

#### Receipt item quantity

The `quantity` field contains the quantity of units in the receipt row. The value of the `quantity` field is a string. If omitted, the implied value is `1`.

#### Receipt item tap

The `tap` field contains an action that may be activated when a user clicks on a non-button or otherwise non-interactive part of a card. The value of the `tap` field is of type [`cardAction`](#Card-action).

### Fact

A `fact` is a single row to be shown within the [`facts`](#Receipt-card-facts) field of a [receipt card](#Receipt-card).

#### Fact key

The `key` field contains the nominative component of the fact. The value of the `key` field is a string.

#### Fact value

The `value` field contains the objective component of the fact. The value of the `value` field is a string.

## References

1. [Activity Protocol Schema](./protocol-activity.md)
2. [Adaptive Cards](https://adaptivecards.io)
3. [RFC 2119](https://tools.ietf.org/html/rfc2119)
4. [MIME media types](https://www.iana.org/assignments/media-types/media-types.xhtml)
5. [RFC 2397](https://tools.ietf.org/html/rfc2397)
6. [ISO 8601](https://www.iso.org/iso-8601-date-and-time-format.html)

# Appendix I - Changes

## 2024-11-01 - trboehre@microsoft.com
* Dropped 'Bot Framework' branding
* Dropped Swagger definitions references

## 2018-10-08 - dandris@microsoft.com
* Add [`duration`](#Media-card-duration) field

## 2018-07-05 - dandris@microsoft.com
* Initial draft
