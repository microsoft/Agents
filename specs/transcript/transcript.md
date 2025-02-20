# Transcript

## Abstract

The Transcript schema is log of conversational actions made by humans and automated software. Transcript extends the Activity Protocol schema to include tracing generated during the processing of those activities.

This schema is used within Agents SDK and tools, and may be consumed or emitted by other software that understand this format.

## Table of Contents

1. [Introduction](#Introduction)
2. [Basic transcript structure](#Basic-transcript-structure)
3. [Serialization](#Serialization)
4. [Activity schema in transcript](#Activity-schema-in-transcript)
5. [References](#References)
6. [Appendix I - Changes](#Appendix-I---Changes)

## Introduction

### Overview

The Transcript schema represents conversational behaviors made by humans and automated software (typically within chat applications) and corresponding processing artifacts, such as natural-language processing results or calls to APIs. Transcript extends the [Activity Protocol](../activity/protocol-activity.md) format [[1](#References)] to include structured extensibility points. It also provides a format for serializing collections of Activities, which is not provided by the core activity schema.

This specification contains additions to the core Activity schema, and includes new fields, specific guidance for existing Activity fields when used within Transcript, and minimal explanations required for understanding Activity fields in context. For a complete description, see the [Activity Protocol](../activity/protocol-activity.md) specification [[1](#References)].

### Requirements

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED", "MAY", and "OPTIONAL" in this document are to be interpreted as described in [RFC 2119](https://tools.ietf.org/html/rfc2119) [[2](#References)].

An implementation is not compliant if it fails to satisfy one or more of the MUST or REQUIRED level requirements for the protocols it implements. An implementation that satisfies all the MUST or REQUIRED level and all the SHOULD level requirements for its protocols is said to be "unconditionally compliant"; one that satisfies all the MUST level requirements but not all the SHOULD level requirements for its protocols is said to be "conditionally compliant."

### Numbered requirements

Lines beginning with markers of the form `TXXXX` are specific requirements designed to be referenced by number in discussion outside of this document. They do not carry any more or less weight than normative statements made outside of `TXXXX` lines.

`T1000`: Editors of this specification MAY add new `TXXXX` requirements. They SHOULD find numeric `TXXXX` values that preserve the document's flow.

`T1001`: Editors MUST NOT renumber existing `TXXXX` requirements.

`T1002`: Editors MAY delete or revise `TXXXX` requirements. If revised, editors SHOULD retain the existing `TXXXX` value if the topic of the requirement remains largely intact.

`T1003`: Editors SHOULD NOT reuse retired `TXXXX` values. A list of deleted values MAY be maintained at the end of this document.

### Terminology

activity
> An action expressed by a bot, a channel, or a client that conforms to the Activity schema.

transcript
> A collection of Activities that conforms to the Transcript schema.

channel
> Software that sends and receives activities, and transforms them to and from chat or application behaviors. Channels are the authoritative store for activity data.

bot
> Software that sends and receives activities, and generates automated, semi-automated, or entirely manual responses. Bots have endpoints that are registered with channels.

transmitted activity
> Transmitted activities are sent between bots and channels, or other services that send and receive activity objects. They represent a request or directive.

metadata activity
> Metadata activities are generated during the process of another operation (typically a transmitted activity), but are neither requests nor directives.

emitter
> Software generating an activity within a transcript.

processor
> Software reading an activity within a transcript.

field
> A named value within an activity or nested object.

### Overall organization

The transcript format is designed to contain lists of related activities. Typically these activities are related to the same conversational interaction, but they could be grouped by other characteristics, such as the user sending the activity, or whether a call to a dependent service succeeded or failed.

Activities within the transcript format are either "transmitted" (in that they went sent or received as a part of a conversation), or "metadata" (in that they were generated, possibly by a participant in the conversation, but not necessarily sent to or received by another party). Typically, metadata activities carry tracing, debugging, and logging data.

The transcript format defines some additional activity types and fields within the activity structure to aid processing of metadata activities.

## Basic transcript structure

This section defines the requirements for the basic structure of the transcript format.

Transcripts are ordered collections of Activity objects. JSON is used as the common interchange format and defines limitations for field contents and uniqueness. (For example, XML-style attributes on field are not allowed.) Implementers will find detailed requirements within the Activity Protocol specification.

`T2000`: Transcript contents MUST be serializable to the JSON format, including adherence to e.g. field uniqueness constraints.

`T2001`: Transcripts MUST contain only child activities, as defined by [Activity Protocol](../activity/protocol-activity.md) [[1](#References)]. Activities MUST adhere to the requirements of that specification for the transcript to be valid.

`T2002`: Processors MAY allow improperly-cased field names, although this is not required. Processors MAY reject activities that do not include fields with the proper casing.

`T2003`: Processors MAY reject activities that contain field values whose types do not match the value types described in this specification.

`T2004`: Unless otherwise noted, emitters SHOULD NOT include empty string values for string fields. Emitters MAY ignore this rule when storing activities emitted by another participant.

`T2005`: Unless otherwise noted, emitters MAY include additional fields within the activity or any nested complex objects. Processors MUST accept fields they do not understand.

`T2006`: Processors MAY ignore activities of types they do not understand.

`T2007`: Processors MAY read common fields of activities even if they do not understand the type of the activity, unless the activity does not contain a `type` field.

`T2008`: Processors SHOULD NOT process any children of the transcript that do not have a `type` field.

`T2009`: Unless otherwise noted, emitters SHOULD NOT include empty arrays or complex objects. Emitters MAY ignore this rule when storing arrays and objects emitted by another participant.

## Serialization

Transcripts are valid either in a well-defined ".transcript" file format, defined in this section, or as a custom-serialized collection of activities that adhere to the same characteristics. The .transcript file format is useful when using other transcript-compatible tools, such as editors and analytics tools, and custom serialization is useful when saving to an application-specific store, like a database.

### .transcript file format

The .transcript format is JSON and allows emitters to choose whether to generate a flat array of activities or a complex object containing a flat array. This choice allows for both efficiency and extensibility.

`T2100`: Valid .transcript files MUST be a serialized JSON entity. That entity MUST be either a flat array of activities or a complex object containing a `transcript` field.

`T2101`: Processors MUST support both the array- and the object-based serialization described in `T2100`.

`T2102`: Emitters SHOULD use UTF-8 encoding for all .transcript files. Emitters SHOULD NOT include byte-order marks (BOM) in .transcript files. Processors MAY reject .transcript files that include BOM or encoding other than UTF-8.

There is no inherent relationship between a .transcript file and criteria used for including content within it. An emitter may choose to write a .transcript file containing activities from a specific conversation, user, or which meet custom predicates (such as conversations that resulted in poor service latency). Emitters may also split a single collection of activities into multiple .transcript files to e.g. keep file sizes small.

`T2103`: Processors SHOULD allow multiple .transcript files to be loaded for each operation.

### Custom storage

In addition to the [.transcript format](#.transcript-file-format), many implementations will serialize Transcript-compatible Activities into storage specific to their application, such as a database or key-value store. The conventions and formats may be specific to this custom storage, but the resulting data is considered to be compatible with Transcript as long as it meets the non-serialization requirements imposed by this format. Of course, import to and export from custom storage is left to the implementer.

### Attachments

The activity schema allows three kinds of attachments:
1. Attachments expressed inline in JSON or JSON-compatible form (e.g. [Adaptive Cards](https://adaptivecards.io/) [[3](#References)])
2. Binary attachments serialized as Data URIs, as defined in [RFC 2397](https://tools.ietf.org/html/rfc2397) [[4](#References)]
3. Binary attachments referenced by URL

Typically attachments of the first two types are self-contained and are meaningful when presented along with the activity containing them. Attachments of the third type reside remotely and may become inaccessible due to permissions, automatic content cleanup, and inadvertent deletion.

`T2200`: Writers SHOULD preserve JSON-compatible and Data URI attachments emitted inline in the JSON payload.

`T2201`: Writers MAY update attachment URIs to reference archived versions of original attachment content.

## Activity extensions

Transcript aims to faithfully reproduce content expressed within the Activity schema. In some cases, the stored form of these activities must be augmented or modified to retain their meaning when the activities are archived within storage.

For example, a [`timestamp`](../activity/protocol-activity.md#Timestamp) is not strictly required as part of an activity when transmitted because both the sender and the receiver have an internal timestamp correlated with the activity transmission. However, in stored form, a timestamp becomes necessary to reconstruct the meaning that was self-evident at runtime.

Additionally, some fields become less important. For example, the [`channelId](../activity/protocol-activity.md#Channel-ID) field is required at runtime for activity receivers to understand the meaning and equivalency of ID fields, but may not be required when processing a single transcript when it is safe to assume they come from the same source. This allows the synthesis of transcripts from sources that did not contain compatible Activities (e.g. human chat logs).

`T2300`: Unless otherwise stated in this document, emitters SHOULD include all available activity fields when creating transcripts.

`T2310`: If the [`timestamp`](../activity/protocol-activity.md#Timestamp) field is not present on an activity, and the emitter knows the exact timestamp when an activity was sent or received, the emitter SHOULD include the timestamp in the activity.

`T2310`: Requirement [`R2020`](../activity/protocol-activity.md#Channel-ID) stating that `channelId` is a mandatory field MAY be ignored when emitting or processing activities. Emitters SHOULD include `channelId` if available per `T2300`.

## References

1. [Activity Protocol](../activity/protocol-activity.md)
2. [RFC 2119](https://tools.ietf.org/html/rfc2119)
3. [Adaptive Cards](https://adaptivecards.io)
4. [RFC 2397](https://tools.ietf.org/html/rfc2397)

# Appendix I - Changes

## 2024-12-22 - trboehre@microsoft.com
* Fixed links
* Dropped Bot Framework moniker

## 2021-10-26 - chrimc@microsoft.com
* Fixed activity links

## 2018-07-05 - dandris@microsoft.com

* Added `T2009`
* Fixed typos

## 2018-06-15 - dandris@microsoft.com

* Initial draft
