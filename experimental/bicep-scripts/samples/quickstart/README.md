## Quickstart Resource Deployment

This folder contains a script `provision.ps1` that can be used to deploy the necessary Graph and Azure resources to run the Quickstart sample.

The script first creates a Service Principal and an associated App Registration. Then, it creates an Azure Bot Service resource tied to the created SP and App.

The output `App Id` and `App Secret` values are tied to the App Registration and can be used to configure the environment to run the sample.