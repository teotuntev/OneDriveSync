# OneDriveSync

A .NET 8 console app that checks whether a file comes back from OneDrive exactly as it went in.

For one file it does this:

1. Hashes the local file with SHA-256.
2. Uploads it to a folder in my OneDrive through Microsoft Graph.
3. Deletes the local copy.
4. Downloads the file back to the same path.
5. Hashes the downloaded file and compares the two hashes.

It prints MATCH or MISMATCH. The exit code is 0 on a match and 1 on a mismatch or any expected failure (sign-in cancelled or timed out, Graph error, network problem, missing file or missing config). Expected failures print one readable message instead of a stack trace.

## Setup

You need the .NET 8 SDK and a personal Microsoft account with OneDrive. You also need to register an app in Microsoft Entra. An Azure subscription is not required for that, but the account needs an Entra directory to hold the registration.

Registering the app:

- In the Entra admin center, open App registrations and choose New registration.
- For supported account types, pick the option that includes personal Microsoft accounts. The app signs in with the consumers authority, so this is required.
- Add a redirect URI of type "Mobile and desktop applications" with the value http://localhost.
- Under Authentication, turn on "Allow public client flows" and save.
- Under API permissions, add Microsoft Graph, Delegated, Files.ReadWrite. No admin consent is needed for a personal account, you just accept the consent prompt on first sign-in.
- Copy the Application (client) ID from the Overview page.

There is no client secret because the app is a public client.

Then, from the project folder (OneDriveSync/OneDriveSync), store the client ID in user secrets so it stays out of the repo:

```powershell
dotnet user-secrets set "Graph:ClientId" "<your client id>"
```

## Running

From the project folder:

```powershell
dotnet run
```

A browser window opens for sign-in. After you sign in and accept the prompt, the round trip runs. Example output:

```
Drive ID: <drive id>
Owner: <your name>
Folder already exists: OneDriveSyncTest
Downloaded file to local disk.
Uploaded test.txt, local 14 bytes, remote 14 bytes, item ID <item id>
Original SHA-256:   40A66E8FACBBE0AB4EAA8F95CC6378CC982909629A0A787222FFF549261F73EF
Downloaded SHA-256: 40A66E8FACBBE0AB4EAA8F95CC6378CC982909629A0A787222FFF549261F73EF
MATCH: File contents are identical.
```

On the very first run the folder line says "Folder created" instead.

```
Drive ID: <drive id>
Owner: <your name>
Folder created: OneDriveSyncTest
Downloaded file to local disk.
Uploaded test.txt, local 14 bytes, remote 14 bytes, item ID <item id>
Original SHA-256:   40A66E8FACBBE0AB4EAA8F95CC6378CC982909629A0A787222FFF549261F73EF
Downloaded SHA-256: 40A66E8FACBBE0AB4EAA8F95CC6378CC982909629A0A787222FFF549261F73EF
MATCH: File contents are identical.
```

The sample file is samples/test.txt. It gets copied into the build output and the app works on that copy, so the original in samples is never touched (a rebuild brings the copy back if it was changed). The remote folder name (OneDriveSyncTest) and the file name are constants in Program.cs. The folder is created in the root of the drive if it is missing, and running the app again replaces the remote file instead of making a duplicate.

## Tests

From the solution folder:

```powershell
dotnet test
```

The tests don't need a sign-in or any network. They run the verification logic against a fake in-memory storage and use a temporary folder for their files. They check the hasher against a known SHA-256 value, a normal match, a corrupted download being reported as a mismatch, the local file being deleted before the download starts, the file ending up back at its original path, and an upload failure leaving the local file untouched.
The OneDrive code itself is not unit tested because that would need a live connection. I tested it by hand with the 14 byte sample and with a 15 MB random file.

## Why I did it this way

I put the cloud part behind a small interface (ICloudStorage with upload and download). OneDriveStorage implements it and RoundTripVerifier only knows the interface. That keeps the verification logic separate from Graph, lets me test it with a fake, and means another provider could be added without touching the verifier.

The local copy is deleted before the download on purpose. If OneDrive changed the content, the hashes would differ either way, so the delete is not what catches corruption. What it protects against is a false MATCH: if the original stayed in place, a download that silently failed to write, or wrote to the wrong place, would still end in a match. Deleting the file first means the check can only pass if the download really recreated it. The delete only happens after the upload succeeded, so a failed upload never costs you the original.

I used the Graph SDK with Azure.Identity instead of raw HTTP to keep the code focused on the verification. Underneath it is the usual Graph calls: get item by path, PUT to content, createUploadSession, and GET content.

Files up to 4 MB are uploaded with a single PUT. Anything bigger goes through an upload session in 1.25 MiB slices (Graph needs a multiple of 320 KiB) and prints progress. Both paths replace an existing file with the same name.

The download is written to a temporary file ending in .download and only moved into place once it finishes, so a dropped connection can't leave a half written file at the real path.

Sign-in is interactive on every run and has a 2 minute timeout. I left out a token cache on purpose so that cancelling the sign-in or running without internet is easy to reproduce. Without the timeout, closing the browser tab leaves the app waiting forever.

## Things that went wrong along the way

Closing the sign-in tab made the app hang with no error. I fixed it with a timeout and a handler for the cancellation.

When I first added the timeout as a separate token request, the sign-in prompt showed up twice because the scope string differed from the one the Graph client used. I fixed it by putting the timeout on the first Graph call instead.

During a 15 MB test the connection dropped in the middle of the download. The old code wrote straight to the real file, so a truncated file was left behind, and the next run treated it as the original and uploaded it. That is why the download now goes through a temporary file.

## Known limitations

- Network errors are reported but not retried.
- If a download fails after the local copy was deleted, the copy in the build folder is gone until the next build. The original in samples is not affected.
- It handles one file per run, and the file and folder names are constants rather than command line options.
- Only personal Microsoft accounts are supported.
- The storage class prints some progress lines directly to the console.
- A token cache would be a sensible addition for regular use.