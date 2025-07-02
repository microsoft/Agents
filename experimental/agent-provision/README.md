1. Open a command window.

1. Sign in to Azure.
   ```azurecli
   az login
   ```
   - A browser window will open. Complete the sign-in process.
   - On success, the command outputs a list of the subscriptions your account has access to.

1. To set the subscription to use, run:
   ```azurecli
   az account set --subscription "<subscription>"
   ```
   - For <subscription>, use the ID or name of the subscription to use.