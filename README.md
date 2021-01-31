# twilio-send-verify

A function to send a verification codes using [Twilio's Verify API](https://www.twilio.com/docs/verify/api) with [OpenFaaS](https://www.openfaas.com/).

Refer to [twilio-check-verify](https://github.com/kylos101/twilio-check-verify) to check verification codes.

## Overview

This function requests Twilio to send a Verification Code via a channel:

- sms
- call
- email (requires extra setup in Twilio, which is not covered here)

When the verification code arrives (which is super fast) it looks like this:

```
Your <COOL_APP_NAME> verification code is 123456.
```

## A Sample Use Case

Let's pretend you have another function written in Python that does user profile updates, and it handles the following scenario.

Given a request to update a user's profile, when the profile update includes opting into receive notifications via SMS, then we want to send a verification code using SMS to the user's phone.

Sending such a request to `twilio-send-verify` to send a verification code may look like:

```python
# I opted to use async, so a failure on the Twilio side doesn't cause my user update to fail
url = "http://gateway.openfaas.svc.cluster.local:8080/async-function/twilio-send-verify.openfaas-fn"
body = {"To": "15551234567", "Channel": "sms"}
response = requests.post(url, json=body)
response.raise_for_status()
```

This will result in the end-user receiving a message with a verification code from Twilio (you can customize the message in Twilio).

## Installation

1. Create a Verify service in [Twilio console](https://www.twilio.com/console/verify/services). Document the `Service ID`, you'll need it in step 4.

2. Save the `AccountSid` and `AuthToken` from Twilio as a secret in Kubernetes

```bash
# get these from https://www.twilio.com/console
TWILIO_ACCOUNT_SID=<YOUR_ACCOUNT_SID>
TWILIO_AUTH_TOKEN=<YOUR_AUTH_TOKEN>
faas-cli secret create twilio-account-sid --from-literal=$TWILIO_ACCOUNT_SID
faas-cli secret create twilio-auth-token --from-literal=$TWILIO_AUTH_TOKEN
```

3. Make sure you have the `csharp-httprequest` template on your machine

```bash
faas-cli template store pull csharp-httprequest
```

4. Configue it to your liking and ship it to OpenFaaS

```yaml
version: 1.0
provider:
  name: openfaas
  gateway: http://127.0.0.1:8080
functions:
  twilio-send-verify:
    lang: csharp-httprequest
    handler: ./twilio-send-verify
    secrets:
      - twilio-account-sid
      - twilio-auth-token
    labels:
      com.openfaas.scale.zero: false
    environment:
      twilio_verify_endpoint: https://verify.twilio.com/v2/Services/<YOUR_VERIFY_SERVICE_ID>/Verifications
```
