# Playtest data: setup and reading it

The game can send anonymous playtest data (Android and Windows builds) to a **Google Sheet** through a small **Google Apps Script web app** that you set up once. The script sorts each event into its own tab and a **Dashboard** tab shows totals and charts. Nothing is sent unless the build has the playtest config **and** the tester pressed ALLOW. Editor runs never send (unless you ask for it, see step 5).

(A Google Form was tried first, but Google refuses scripted posts to forms. An Apps Script web app is the supported way for an app to write to a sheet.)

## 1. Set up the receiver (about 5 minutes, once)

1. Open the Google Sheet you want the data in.
2. Choose **Extensions > Apps Script**. A script editor opens.
3. In the project folder, open **`Docs/playtest_receiver.gs`** in your editor, select all (Ctrl+A) and copy. In the Apps Script editor, delete everything, paste, and press **Save**. (Copy from the file, not from a chat or a web page: stray quote or backtick characters break the script.)
4. Build the tabs: in the toolbar's function menu pick **`setupSheets`** and press **Run**. The first time, Google asks you to authorize: choose your account; if it says "Google hasn't verified this app", click **Advanced > Go to (project name) (unsafe) > Allow**. It is your own script. When it finishes, go back to the sheet: it now has the tabs **Dashboard, Stages, Operators, Sessions, Feedback, Errors, Tables, Log**, and any old test rows are gone.
5. Deploy it: **Deploy > New deployment**, click the gear next to "Select type", choose **Web app**, and set:
   - **Execute as:** **Me**
   - **Who has access:** **Anyone** (not "Anyone with Google account")

   Click **Deploy**, then copy the **Web app URL** (it ends in `/exec`).
6. Check it: paste the URL into a browser tab. You should see `TRASH TD playtest receiver is running`.

> **Changing the script later:** paste the new file, Save, then **Deploy > Manage deployments > pencil > Version: New version > Deploy**. Saving alone does not update the live web app, and the address stays the same.
>
> **Starting over:** when you reopen the sheet there is a **TRASH TD** menu with **Rebuild sheets and charts**. It erases all collected rows and rebuilds the tabs.

## 2. Put it in the game

1. Copy the web app URL and in Unity choose **TRASH TD > Playtest > Create Config From Copied Web App Link**.
2. It creates `Assets/Resources/PlaytestConfig.json`. **Git ignores this file on purpose**: it holds your web app address, so it stays on your computer and teammates' own test runs do not fill your sheet. Do not commit it or paste it in public.
3. Every build you make **from now on** contains the config. A build made without the file asks nothing and sends nothing.

## 3. Test it

1. Choose **TRASH TD > Playtest > Send Test Row**. A dialog tells you what Google answered, and a `test` row appears in the **Log** tab.
2. **TRASH TD > Playtest > Show Status** (while the game runs) says in plain words why events are or are not being sent.

## 4. Send the builds

- **Android:** the `.apk` (TRASH TD > Android > Build Android APK).
- **Windows:** the build folder, zipped.

Tell testers: "On first start it asks if you want to share anonymous data. You can change it in Settings > Audio > Playtest. Use the SEND button there to leave feedback."

## 5. Try it in the editor

1. In `PlaytestConfig.json`, set `"sendFromEditor": true`.
2. Start the game from the main menu. After about two seconds a **HELP US PLAYTEST** box appears. Press **ALLOW**.
3. Play a stage for a minute, then leave to the menu. Within about 10 seconds you should see rows appear.
4. Set `sendFromEditor` back to `false` afterwards, so your own test runs do not fill the sheet.

## What is in the sheet

| Tab | What it holds |
|---|---|
| **Dashboard** | Totals (testers, launches, stage runs, clear rate, average rating, average fps, errors) and eight charts: how each stage ends, minutes to clear, lives lost, ratings, launches by platform, frame rate by device, most drafted operators, operator clear rate. It reads the other tabs with formulas, so it updates by itself. |
| **Stages** | One row per finished stage run: tester, platform, device, build, stage, difficulty, **result** (clear, fail or quit), stars, seconds, wave reached, lives left and lost, rerolls, average fps, hitches, worst frame, and which operators were used, lost and drafted. Results and slow frame rates are colour coded. |
| **Operators** | One row per operator with running totals: times drafted, deployed, retreated, lost, stages used, stages cleared, and the clear rate. |
| **Sessions** | One row per launch: device, OS, RAM, GPU, screen, refresh rate, the options the tester uses (controls, CRT, UI size, fps cap, picture). |
| **Feedback** | Star ratings and notes, with the stage they were sent from. |
| **Errors** | Script errors the game caught (at most 8 per launch). |
| **Tables** | The summary tables behind the charts. You can look, but do not edit. |
| **Log** | Every event as the game sent it. The backup. |

The **Tester** column is a random install id (the first part of the session id): rows with the same tester are the same device. **No name, email, account or location is collected.**

## What the game sends

| Event | When | Goes to |
|---|---|---|
| `session_start` | once per launch | Sessions |
| `stage_start` | a stage begins | Log |
| `stage_end` | cleared, failed or left (`result` is `clear`, `fail` or `quit`); includes the CRT style, and on phones the FPS cap and picture size, in force at that moment | Stages and Operators |
| `feedback` | tester presses SEND in Settings | Feedback |
| `error` | a script error in the game | Errors |

## Frame rate per graphics style

The **Stages** tab has three last columns, **CRT** (Off, Classic or Full TV), **FPS cap** and **Picture** (phones only), next to **Avg FPS** and **Device**. To compare styles on a device, filter or pivot the Stages tab on those columns. A sheet created before they existed keeps working: type `CRT`, `FPS cap` and `Picture` into cells V1, W1 and X1 of the Stages tab and redeploy the script (Deploy > Manage deployments > edit > New version). Do not run `setupSheets()` for this, it erases the rows. The same values are in the **Log** tab either way.

## Good to know

- **Offline testers:** events wait in a small file on the phone and are sent when it is online again (at most 300 are kept).
- **The web app address is inside every build.** Anyone who takes the build apart could send fake rows. If your sheet ever fills with junk, deploy a **new** web app (Deploy > New deployment), put the new address in the config and rebuild; delete the old deployment.
- **Turning it off for everyone:** delete `PlaytestConfig.json` and rebuild. The playtest system then does not exist in that build.
- **Privacy:** the consent box says what is sent. If you add new events, keep them anonymous and update that text (`PlaytestUi.ShowConsent`).
- **Charts and formatting** are built by Google's own functions, which could only be checked against a stand-in here. If a chart looks wrong, tell Claude what you see.
