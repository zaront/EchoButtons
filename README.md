# Use Echo Buttons in windows with .NET

## Getting Started
### Install from nuget
https://www.nuget.org/packages/EchoButtons

Make sure you pair your buttons with Windows first, or they won't be recognized.


## Basic code examples
```cs
var button = new EchoButton();
button.Pressed += (sender, e) =>
{
	//button pressed
};
button.StartListening();
```

The API support multiple buttons and returns a unique buttons description for each device.

It doesn't yet support setting the colors.  I havn't figure that out yet.

If anyone know how to do that, or has seen documentation on it.  Please share it with me and I will add that fuctionality too.

Enjoy!