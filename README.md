# Unturned Conflict Scanner

This doesn't have full CLI support, but if someone actually wants it let me know and I'll add it. This was just a quick tool I made for finding conflicting IDs and I thought I would publish it.

## Usage
If you don't pass a path as a launch argument, you will be prompted to enter a base folder.
It must be an absolute path to a folder.

Next, the folder will be scanned for all assets. This includes the newer v2 (`.asset`) format and the older v1 (`.dat`) format.

You will be prompted for an operation.

## Free UInt16 ID
Type in `free <category> <lower ID bound>`.

Category corresponds to EAssetType from `SDG.Unturned` and can be any of the following.
* Item
* Effect
* Object
* Resource
* Vehicle
* Animal
* Mythic
* Skin
* Spawn
* NPC

Each category has its own separate ID database and therefore can have overlapping IDs, which is why you must specify.

Next, enter your lowest ID. Every ID including and after this one will be checked to see if it's taken.

The first one that isn't taken will be printed and copied to the clipboard.

![image](https://github.com/DanielWillett/ConflictScanner/assets/12886600/7c65c4b4-7920-4e36-a9eb-8dda1e452d11)


## GUID
A unique GUID will be generated and checked against the mod and regenerated until it isn't a duplicate GUID (extremely unlikely this will ever happen). It will then be copied to your clipboard.

![image](https://github.com/DanielWillett/ConflictScanner/assets/12886600/3d0c1637-0ecf-48bb-a7a3-eb1780534a3b)

## Find
Use a 'fuzzy search' algorithm to find assets by name, GUID, or ID.

Syntax:
`find [type] search...`

Note that results are returned reversed and will return as many as will fill the console.

### Examples (screenshots from vanilla result set):

#### Search all vehicles for 'hatchback'.
![Screenshot 2025-03-20 180926](https://github.com/user-attachments/assets/e34f6d08-328c-48d3-8d57-921b0d0749e0)

`find vehicle hatchback`

#### Search all ItemGunAssets for 'peace'. 
![Screenshot 2025-03-20 181207](https://github.com/user-attachments/assets/0be2538d-6249-4fa5-afef-8ee8406290d9)

`find gun peace`

#### Search all assets for 'gun'.
![image](https://github.com/user-attachments/assets/0021178a-2b5c-4467-909e-9a241eabd923)

`find _ gun`
gun is an asset type so by itself it would select all guns

#### Search all vehicle redirect assets for 'green'. 
![image](https://github.com/user-attachments/assets/94a8288a-175a-45b6-bf3d-b881dada1f92)

`find VehicleRedirector green`

#### Find all assets with ID 193, then search all assets by '193'.
![Screenshot 2025-03-20 181326](https://github.com/user-attachments/assets/59f5b32b-5403-4657-9284-5c458186d0da)

`find 193`

#### Find only assets with GUID 2a33663da0ab461abe9cb545426d6b3d. 
![Screenshot 2025-03-20 181520](https://github.com/user-attachments/assets/54eddf2d-4bc1-4ccb-91e7-e8179ebb1e8b)

`find 2a33663da0ab461abe9cb545426d6b3d`

#### Conflicts
Will scan all loaded files for assets that are either:
* Missing a GUID
* Have a duplicate GUID
* Have a duplicate ID within their category

Violating files will be printed with their name and path relative to the selected folder.

![image](https://github.com/DanielWillett/ConflictScanner/assets/12886600/d8f195be-651f-4116-b6fb-9d6909e8886e)

### Other Commands
* `refresh` - Rescan the same path.
* `path` - Enter a new path and refresh.
* `exit` or `quit` - Close the application.
