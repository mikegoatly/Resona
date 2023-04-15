# Resona
An audiobook/music/sleep tunes audio player designed to run on a Raspberry PI

This is very much a work in progress limited spare time hobby project... have patience young padawan. 

# Dev notes

Work on the core database structure is ongoing, so I'm just going to recreate the initial database structure every time I make a change using:

``` powershell
cd src\Resona.Persistence
rm -r Migrations
md Migrations
dotnet ef migrations add InitialCreate --startup-project ..\Resona.UI -o Migrations
```
