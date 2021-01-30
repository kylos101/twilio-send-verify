# twilio-send-verify

A function to send verify requests via [Twilio's Verify API](https://www.twilio.com/docs/verify/api) using [OpenFaaS](https://www.openfaas.com/) on [Kubernetes](https://kubernetes.io/).

It is outside the scope of this function to provide a mechanism to assert the codes delivered to devices by Twilio. I'll post a separate function for that use case.

## Installation

1. Create a Verify service in [Twilio console](https://www.twilio.com/console/verify/services). Document the `Service ID`, you'll need it in step 4.

2. Save the `AccountSid` and `AuthToken` from Twilio as a secret in Kubernetes

```bash
# get these from https://www.twilio.com/console
export TWILIO_ACCOUNT_SID=
export TWILIO_AUTH_TOKEN=
kubectl create secret \
  generic twilio-creds -n openfaas-fn \
  --from-literal=accountSid=$TWILIO_ACCOUNT_SID \
  --from-literal=authToken=$TWILIO_AUTH_TOKEN
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
      - twilio-creds
    labels:
      com.openfaas.scale.zero: false
    environment:
      twilio_verify_endpoint: https://verify.twilio.com/v2/Services/<YOUR_VERIFY_SERVICE_ID>/Verifications
```

## Usage

This function receives a request to ask Twilio to send a Verify code to a device via:

- sms
- call
- email (requires extra setup in Twilio, which is not covered here)

## A Sample Use Case

Let's pretend you have another function written in Python that does user profiles updates.

Depending on the content in the update (maybe the user opted-in to receive notifications via SMS), we may want to start a workflow to send a verification code to the user's mobile number via this `twilio-send-verify` function.

Sending such a request may look like this:

```python
# I opted to use async, so Twilio failures don't cause my user update to fail
url = "http://gateway.openfaas.svc.cluster.local:8080/async-function/twilio-send-verify.openfaas-fn"
body = {"To": "15551234567", "Channel": "sms"}
response = requests.post(url, json=body)
response.raise_for_status()
```

This will result in the end-user receiving a message with a verification code from Twilio (you can customize the message in Twilio).
