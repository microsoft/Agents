// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ProductionReference;

await using var app = AppHost.Build(args);
await app.RunAsync();
