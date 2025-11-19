# C#/.NET Onion HTTP Client Library

A simple HTTP client that routes requests through The Onion Router (Tor) protocol using local Tor proxies.

## Requirements

- .NET 10.0 or later
- Tor expert bundle installed

  - macOS: `brew install tor` (via Homebrew)
  - Ubuntu: `sudo apt install tor`
  - Windows: Currently not supported

  **Note:** Tor service does not need to be running as each Tor HTTP client (class `TorHttpClient`) will include its own instance.

## Configuration

Create a Tor control password by running `tor --hash-password <YOUR_PLAIN_TEXT_PASSWORD>`.

Create `torrc-defaults` file in the same directory as the executable with the following content:

```
HashedControlPassword <YOUR_HASHED_PASSWORD>
```

Create 'onionhttpclient.secrets.json' in the same directory as the executable with the following content:

```json
{
  "Tor": {
    "ControlPassword": "<YOUR_PLAIN_TEXT_PASSWORD>"
  }
}
```

## Usage

### Single Tor HTTP Client (class `TorHttpClient`)

Manages an internal HTTP client and Tor service.

```csharp
using Kokoabim.OnionHttpClient;

hostBuilder.AddOnionHttpClient();

...

var httpClientSettings = new HttpClientInstanceSettings()
{
    ...
};

var torSettings = new TorInstanceSettings();

using var torHttpClient = host.Services.GetRequiredService<ITorHttpClient>();
await torHttpClient.InitializeAsync(httpClientSettings, torSettings);

var response = await torHttpClient.SendAsync("https://torproject.org");

...

await torHttpClient.DisconnectAsync();
```

### Multi Tor HTTP Client (class `MultiTorHttpClient`)

Manages multiple internal HTTP clients and Tor services and balances requests between them.

```csharp
using Kokoabim.OnionHttpClient;

hostBuilder.AddOnionHttpClient();

...

var multiTorHttpClientSettings = new MultiTorHttpClientSettings
{
    BalanceStrategy = MultiTorHttpClientBalanceStrategy.RoundRobin,
    ClientCount = 2,
    ...
};

var httpClientCommonSettings = new HttpClientCommonSettings()
{
    ...
};

using var multiTorHttpClient = host.Services.GetRequiredService<IMultiTorHttpClient>();
await multiTorHttpClient.InitializeAsync(multiTorHttpClientSettings, httpClientCommonSettings);

var response = await multiTorHttpClient.SendAsync("https://torproject.org");

...

await multiTorHttpClient.DisconnectAsync();
```
