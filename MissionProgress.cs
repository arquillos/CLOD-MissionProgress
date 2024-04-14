/// @Author    20XX - ???
/// @Author    2020-2022 - Gabu
/// @Author    2024 - Arquillos
//$reference Campaign.dll
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using maddox.game;
using maddox.game.world; /// AiActor
using maddox.GP;

// string pathToMission = base.PathMyself;            // E.g: parts\bob\mission\campaign\campaign_Eagle_Day\DE_01.mis
// string baseMissionPath = AMission.BaseMissionPath; // E.g: C:\Users\<User>\...\parts\bob\mission\campaign\campaign_Eagle...
// string player = GamePlay.gpPlayer().Name();        // The player name
// string mission = base.MissionFileName;             // E.g: DE_01.mis
// string missionName = mission.Replace(".mis", String.Empty);

public class CampaignInfo
{
	string mPath = "";
	public string campaignName = "";
	public int missionNumber = 0;

	public CampaignInfo(string path)
	{
		mPath = path;
		campaignName = CampaignName();
		missionNumber = MissionNumber(campaignName);
	}

	public string CampaignName()
	{
		string pathToMission = mPath; // E.g: parts/bob/mission/campaign/campaign_Eagle_Day/DE_01.mis
		string[] pathSuffix = pathToMission.Split('/');
		return pathSuffix[5];
	}

	public int MissionNumber(string campaignName)
	{
		// Read the Index from the <My Docs>\1C SoftClub\il-2 sturmovik cliffs of dover\mission\campaign\bob.<CAMPAIGN NAME>.state.ini file
		// This file is managed by the game and it contains the mission number for the next mission in the campaign
		// The value is used to initialize the campaign score when the player starts a campaign
		// TODO: The game is able to return this value but I don't know how to get it from the API
		string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		string battleIndexFile = mydocpath + @"\1C SoftClub\il-2 sturmovik cliffs of dover\mission\campaign\bob." + campaignName + ".state.ini";
		string line, index;

		using (FileStream fs = new FileStream(battleIndexFile, FileMode.Open, FileAccess.Read))
		{
			// Reads the mission number to play. A 0 value means the first mission of the campaign
			// File example:
			// [Main]
			//   battleIndex 2
			using (StreamReader f2 = new StreamReader(fs))
			{
				line = f2.ReadLine();
				line = f2.ReadLine();
				string[] lineSubElements = line.Split(' ');
				index = lineSubElements[3];
				f2.Close();
			}
		}
		return int.Parse(index);
	}
}

public class Mission : maddox.game.campaign.Mission
{
	// Configuration
	int missionBrienfingMaxLineLength = 500;
	// TODO: Automatically get my squadron name
	static string mySquad = "JG51";
	static string missionObjectiveBombers = "214Sqn";
	static bool displayLogs = true;      // Logs - Display the logs in the Screen
	static double logsDuration = 5.0;    // Logs - Time in secs while the log is displayed
										 // Configuration - Mission completion
										 // Set whatever is needed
										 // 1. The player has landed - Obligatory
										 // 2. My squad is alive (parametrizable: number of planes of my squad that are alive after the battle)
	bool squadObjective = false;
	// 3. Number of Luftwaffe losses vs number of RAF losses
	bool lossesObjective = true;
	// 4. Number of completed waypoints
	bool waypointsObjective = false;
	// 5. Number of bombers shoot down by the Squad (depends on the mission type)
	bool bombersObjective = false;
	// 6. ...


	// Mission status files
	string campaignScoreFile;   // Campaign score file
	string missionBriefingFile; // Mission score file
								// Mission result messages
	string missionSuccessResult = "Excellent! Mission accomplished.";
	string missionFailedResult = "Mission failed.";

	// File score
	int cPlayerKillsScore = 0;             // Air victories
	int cPlayerStaticPlanesKillsScore = 0; // Static victories
	int cPlayerGroundKillsScore = 0;       // Ground targets
	int cPlayerShipKillsScore = 0;         // Ship targets
	int cMySquadLossesScore = 0;           // My squad losses.
	int cFriendlyLossesScore = 0;          // Luftwaffe aircraft destroyed.
	int cEnemyAircraftDownScore = 0;       // RAF aircraft destroyed.

	// Mission score
	int cPlayerKills = 0;                // Mission: Aircraft shoot down
	int cPlayerStaticPlanesKills = 0;    // Mission: Static planes destroyed
	int cPlayerGroundKills = 0;          // Mission: Ground targets detroyed
	int cPlayerShipKills = 0;            // Mission: Ships sunk
	int cFriendlyLosses = 0;             // Luftwaffe aircraft destroyed.
	int cMySquadLosses = 0;              // My squad losses.
	int cEnemyAircraftDown = 0;          // RAF aircraft destroyed.
										 // Mission score (Not saved) - Luftwaffe
	int cPlayerBomberKills = 0;          // Kills member of a specific unit by the player. Eg bombers or Beaufighters units
	int cMySquadLandedSafely = 0;        // My squad aircraft landed.
	int cFriendlyGroundLosses = 0;       // Wehrmacht unit destroyed.
	int cFriendlyBombersLosses = 0;      // Luftwaffe bombers destroyed (From an specific unit).
										 // Mission score (Not saved) - RAF
	int cEnemyShipDestroyed = 0;         // RAF ships destroyed.
	int cEnemyGroundDestroyed = 0;       // RAF ground forces destroyed.
	int cEnemyStaticPlanesDestroyed = 0; // RAF static planes destroyed.

	int cInitialNumberLWPlanes = 0;      // Number of LW aircrafts at the start of the mission
	int cInitialNumberRAFPlanes = 0;     // Number of RAF aircrafts at the start of the mission

	public int GetNumberOfAircraftPerArmy(int army)
    {
		// Get the number of aircrafts at the beginning of the mission by Army
		AiAirGroup[] gpAirGroups = base.GamePlay.gpAirGroups(army);
		int airGroups = gpAirGroups.Length;
		GamePlay.gpLogServer(String.Format("Army: {0}, air groups: {1}", army.ToString(), airGroups.ToString()));
		int totalNumAircrafts = 0;
		for (int i = 0; i < gpAirGroups.Length; i++)
		{
			int id = gpAirGroups[i].ID();
			int numAircrafts = gpAirGroups[i].InitNOfAirc;
			totalNumAircrafts += id;
			GamePlay.gpLogServer(String.Format("Army {0} air group: {1}, Num Init aricraft: {2}", army.ToString(), id.ToString(), numAircrafts.ToString()));
		}

		return totalNumAircrafts;
	}

	public override void OnBattleStarted()
	{
		base.OnBattleStarted();

		CampaignInfo campaignInfo = new CampaignInfo(base.PathMyself);
		string campaignName = campaignInfo.campaignName;
		//GamePlay.gpLogServer(String.Format("Campaign name: {0}", campaignName));
		int missionNumber = campaignInfo.missionNumber;
		//GamePlay.gpLogServer(String.Format("Mission number: {0}", missionNumber));


		string player = GamePlay.gpPlayer().Name(); // The player name
		string mission = base.MissionFileName;      // E.g: DE_01.mis
		string missionName = mission.Replace(".mis", String.Empty);

		// LW - Army 2
		cInitialNumberLWPlanes = GetNumberOfAircraftPerArmy(2);
		// RAF - Army: 1
		cInitialNumberRAFPlanes = GetNumberOfAircraftPerArmy(1);

		// Mission status files
		campaignScoreFile = String.Format(@"parts\bob\mission\campaign\{0}\{1}.Score.txt", campaignName, campaignName);
		//GamePlay.gpHUDLogCenter(String.Format("Campaign: {0}", campaignScore));
		missionBriefingFile = String.Format(@"parts\bob\mission\campaign\{0}\{1}.briefing", campaignName, missionName);
		//GamePlay.gpHUDLogCenter(String.Format("Mission briefing: {0}", missionBriefing));

		// First mission of the campaign. Resetting scores.
		if (missionNumber == 0)
		{
			WiteCampaignScore(); // First campaign mission

			// TODO: Customize automatically with the player squad name
			HudLog(String.Format("Welcome {0} to Stab Jagdgeschwader 51", player));
		}
		else
		{
			ReadCampaignScore();
			HudLog("Another mission for the Jagdgeschwader 51, welcome back!");
		}
	}

	private void WiteCampaignScore()
	{
		// Writing the mission score
		using (FileStream fs = new FileStream(campaignScoreFile, FileMode.Create))
		{
			using (StreamWriter f3 = new StreamWriter(fs))
			{
				// Aerial victories
				f3.WriteLine(String.Format("{0}", cPlayerKillsScore + cPlayerKills));
				// Static planes
				f3.WriteLine(String.Format("{0}", cPlayerStaticPlanesKillsScore + cPlayerStaticPlanesKills));
				// Ground targets
				f3.WriteLine(String.Format("{0}", cPlayerGroundKillsScore + cPlayerGroundKills));
				// Ship targets
				f3.WriteLine(String.Format("{0}", cPlayerShipKillsScore + cPlayerShipKills));
				// My squad losses.
				f3.WriteLine(String.Format("{0}", cMySquadLossesScore + cMySquadLosses));
				// Luftwaffe aircraft destroyed.
				f3.WriteLine(String.Format("{0}", cFriendlyLossesScore + cFriendlyLosses));
				// RAF aircraft destroyed.
				f3.WriteLine(String.Format("{0}", cEnemyAircraftDownScore + cEnemyAircraftDown));
				f3.Close();
			}
		}
	}

	private void ReadCampaignScore()
	{
		string line;
		using (FileStream fs = new FileStream(campaignScoreFile, FileMode.Open, FileAccess.Read))
		{
			// Reads the current score of campaign and adds the mission score
			using (StreamReader f2 = new StreamReader(fs))
			{
				// Aerial victories
				line = f2.ReadLine();
				cPlayerKillsScore = Int32.Parse(line);
				// Static planes
				line = f2.ReadLine();
				cPlayerStaticPlanesKillsScore = Int32.Parse(line);
				// Ground targets
				line = f2.ReadLine();
				cPlayerGroundKillsScore = Int32.Parse(line);
				// Ship targets
				line = f2.ReadLine();
				cPlayerShipKillsScore = Int32.Parse(line);
				// My squad losses.
				line = f2.ReadLine();
				cMySquadLossesScore = Int32.Parse(line);
				// Luftwaffe aircraft destroyed.
				line = f2.ReadLine();
				cFriendlyLossesScore = Int32.Parse(line);
				// RAF aircraft destroyed.
				line = f2.ReadLine();
				cEnemyAircraftDownScore = Int32.Parse(line);
				f2.Close();
			}
		}
	}

	private void UpdateBriefing()
	{
		// Updating the mission briefing
		// First - Reading the first part (Intro) of the mission briefing 
		int lineNumber = 1;
		string[] modifiedBriefing = new string[missionBrienfingMaxLineLength];
		StreamReader f1 = new StreamReader(missionBriefingFile);
		string fileLine;
		do
		{
			fileLine = f1.ReadLine();
			modifiedBriefing[lineNumber++] = fileLine;
		}
		while (fileLine != "[2]"); // File part [2] -> "Success"
		f1.Close();

		// Writing the [1] part (Intro) - Not modified -------------------------
		int j = 0;
		StreamWriter f = new StreamWriter(missionBriefingFile);
		while (++j < lineNumber)
		{
			f.WriteLine(modifiedBriefing[j]);
		}

		// Second - Writing the [2] part - Success -----------------------------
		// Mission status (success/failed)
		f.WriteLine("<Name>");
		f.WriteLine("Success");
		f.WriteLine("<Description>");
		f.WriteLine(missionSuccessResult);

		WriteBriefingPart(f);

		// Briefing slide
		f.WriteLine("<Slide>");
		f.WriteLine("MissionSuccess.jpg");
		f.WriteLine("<Caption>");

		// Third- Briefing file Part [3] - Failure -----------------------
		f.WriteLine("[3]");
		f.WriteLine("<Name>");
		f.WriteLine("Failure");
		f.WriteLine("<Description>");
		f.WriteLine(missionFailedResult);

		WriteBriefingPart(f);

		// Briefing slide
		f.WriteLine("<Slide>");
		f.WriteLine("MissionFailure.jpg");
		f.WriteLine("<Caption>");
		f.Close();
	}

	private void WriteBriefingPart(StreamWriter f)
	{
		// Write the mission score
		f.WriteLine("Total score for the Mission:");
		f.WriteLine(String.Format("RAF Aircraft losses: {0} of {1}", cEnemyAircraftDown, cInitialNumberRAFPlanes));
		f.WriteLine(String.Format("LW Aircraft losses: {0} of {1}", cFriendlyLosses, cInitialNumberLWPlanes));
		f.WriteLine(String.Format("Squad losses: {0}", cMySquadLosses));
		if (cPlayerKills > 0)
		{
			f.WriteLine(String.Format("Player - enemy aircraft down: {0}", cPlayerKills));
		}
		if (cPlayerStaticPlanesKills > 0)
			f.WriteLine(String.Format("Player - destroyed static planes: {0}", cPlayerStaticPlanesKills));
		if (cPlayerGroundKills > 0)
			f.WriteLine(String.Format("Player - destroyed ground units: {0}", cPlayerGroundKills));
		if (cPlayerShipKills > 0)
			f.WriteLine(String.Format("Player - sunk ships: {0}", cPlayerShipKills));
		// Option: Display the sum of all the player kills, not only the aricraft
		// f.WriteLine(String.Format("My kills: {0} ", cPlayerKills +  cPlayerStaticPlanesKills + cPlayerShipKills + cPlayerGroundKillsScore));

		f.WriteLine("  ");
		f.WriteLine("Total score for campaign:");
		f.WriteLine(String.Format("RAF losses: {0} ({1}%)", cEnemyAircraftDownScore + cEnemyAircraftDown));
		f.WriteLine(String.Format("Luftwaffe losses: {0} ({1}%)", cFriendlyLossesScore + cFriendlyLosses));
		f.WriteLine(String.Format("Squad losses: {0}", cMySquadLossesScore + cMySquadLosses));
		if (cPlayerKillsScore + cPlayerKills > 0)
			f.WriteLine(String.Format("Player aerial victories: {0}", cPlayerKillsScore + cPlayerKills));
		if (cPlayerStaticPlanesKillsScore + cPlayerStaticPlanesKills > 0)
			f.WriteLine(String.Format("Player ground planes destroyed: {0}", cPlayerStaticPlanesKillsScore + cPlayerStaticPlanesKills));
		if (cPlayerGroundKillsScore + cPlayerGroundKills > 0)
			f.WriteLine(String.Format("Player ground units destroyed: {0}", cPlayerGroundKillsScore + cPlayerGroundKills));
		if (cPlayerShipKillsScore + cPlayerShipKills > 0)
			f.WriteLine(String.Format("Player ships destroyed: {0}", cPlayerShipKillsScore + cPlayerShipKills));
	}

	public override void OnActorDead(int missionNumber, string shortName, AiActor actor, List<DamagerScore> initiatorList)
	{
		// Check if the the player destroyed the ground unit
		bool killedByPlayer = false;
		if (GamePlay.gpPlayer().Place() != null)
		{
			foreach (DamagerScore i in initiatorList)
			{
				if (i.initiator != null && i.initiator.Actor == GamePlay.gpPlayer().Place())
				{
					killedByPlayer = true;
				}
			}
		}

		// Aircrafts
		if (actor is AiAircraft)
		{
			// RAf
			if (actor.Army() == 1)
			{
				if (killedByPlayer)
				{
					// Increments when players kills member of a specific unit eg bombers or Beaufighters units
					if (shortName.IndexOf(missionObjectiveBombers, 0) > 0)
					{
						cPlayerBomberKills++;
						GamePlay.gpHUDLogCenter(String.Format("You shoot down a RAF bomber: {0} {1}", actor.Name(), shortName));
					}
					else
					{
						// Aerial victory
						cPlayerKills++;
						GamePlay.gpHUDLogCenter(String.Format("You shoot down a RAF aircraf: {0} {1}", actor.Name(), shortName));
					}
				}
				else
				{
					cEnemyAircraftDown++;
					GamePlay.gpHUDLogCenter(String.Format("RAF aircraft shoot down: {0} {1}", actor.Name(), shortName));
				}
			}

			// Luftwaffe
			if (actor.Army() == 2)
			{
				cFriendlyLosses++;
				HudLog(String.Format("Luftwaffe down: {0} {1}", actor.Name(), shortName));

				// Player sqn loss
				if (shortName.IndexOf(mySquad, 0) > 0)
				{
					cMySquadLosses++;
				}

				// Losses in unit to protect, can be repeated if several units - just copy and paste the line
				if (shortName.IndexOf("KG3", 0) > 0)
				{
					cFriendlyBombersLosses++;
				}
			}

		}

		// Ground units
		if (actor is AiGroundActor)
		{
			AiGroundActor toto = (AiGroundActor)actor;

			// RAF
			if (actor.Army() == 1)
			{
				// Ships and submarines
				if (((int)toto.Type() >= 30) & ((int)toto.Type() < 38))
				{
					if (killedByPlayer)
					{
						cPlayerShipKills++;
						GamePlay.gpHUDLogCenter(String.Format("You've destroyed a British Ship: {0} {1}", actor.Name(), shortName));
					}
					else
					{
						cEnemyShipDestroyed++;
						GamePlay.gpHUDLogCenter(String.Format("British Ship destroyed: {0} {1}", actor.Name(), shortName));
					}
				}
				// Plane
				else if ((int)toto.Type() == 25)
				{
					if (killedByPlayer)
					{
						cPlayerStaticPlanesKills++;
						GamePlay.gpHUDLogCenter(String.Format("You've destroyed a RAF static plane: {0} {1}", actor.Name(), shortName));
					}
					else
					{
						cEnemyStaticPlanesDestroyed++;
						GamePlay.gpHUDLogCenter(String.Format("RAF static plane destroyed: {0} {1}", actor.Name(), shortName));

					}
				}
				else
				{
					if (killedByPlayer)
					{
						cPlayerGroundKillsScore++;
						GamePlay.gpHUDLogCenter(String.Format("You've destroyed a British ground unit: {0} {1}", actor.Name(), shortName));
					}
					else
					{
						cEnemyGroundDestroyed++;
						GamePlay.gpHUDLogCenter(String.Format("British ground unit destroyed: {0} {1}", actor.Name(), shortName));
					}
				}
			}

			// Luftwaffe
			if (actor.Army() == 2)
			{
				cFriendlyGroundLosses++;
				GamePlay.gpHUDLogCenter(String.Format("Wehrmacht unit destroyed: {0} {1}", actor.Name(), shortName));
			}
		}
	}

	public override void OnAircraftLanded(int missionNumber, string shortName, AiAircraft aircraft)
	{
		// Check for player Aircraft
		if (GamePlay.gpPlayer().Place() == aircraft)
		{
			HudLog("You've landed safely! Well done.");
			checkVictory(aircraft);
			WiteCampaignScore();
			UpdateBriefing();
		}
		else
		// Any other aircraft
		{
			if (aircraft.Army() == 2)
			{
				// Count the landed aircrafts of my squadron
				if (shortName.IndexOf(mySquad, 0) > 0)
				{
					cMySquadLandedSafely++;
					HudLog("Another friend is safe");
				}
				else
				{
					HudLog("A Luftwaffe aircraft has returned home");
				}
			}
			else
			{
				HudLog("A RAF enemy has returned home");
			}
		}
	}

	// When the player has landed we check for battle success
	private void checkVictory(AiAircraft aircraft)
	{
		// 1. The player has landed - Obligatory
		bool missionCompleted = true;

		// 2. My squad is alive (parametrizable: number of planes of my squad that are alive after the battle)
		// Limitation: The Squad is checked without knowing if all the members have landed safely
		if (squadObjective == true)
		{
			// Player squadron losses
			if (cMySquadLosses == 0)
				missionSuccessResult += "Our squadron had no losses!";
			else
			{
				// TODO: Pending geting the number of planes in my squad
				int cSquadComponents = 1000; // At this moment, this check is not useful (The number of Squad components should be smth like 4)
				if (cMySquadLosses <= cSquadComponents)
				{
					missionSuccessResult += String.Format("Our squadron has suffered {0} losses", cMySquadLosses);
				}
				else
				{
					missionFailedResult += String.Format("You did not protect you squad. (Losses: {0})", cMySquadLosses);
					missionCompleted = false;
				}
			}
		}

		// 3. Number of Luftwaffe losses vs number of RAF losses
		if (lossesObjective == true)
		{
			if (cFriendlyLosses == 0)
			{
				missionSuccessResult += "Well, without loss, indeed a good result";
			}
			else if (cFriendlyLosses > cEnemyAircraftDown)
			{
				missionFailedResult += "The Luftwaffe lost more planes thant the RAF.";
				missionCompleted = false;
			}
			else if (cFriendlyLosses < cEnemyAircraftDown)
			{
				missionSuccessResult += "We had a great battle: the enemy had more losses than we did.";
			}
			else
			{
				missionSuccessResult += "Our total losses equals enemy losses. We cannot continue like this: the enemy has more planes than we do.";
			}
		}

		// 4. Number of completed waypoints
		if (waypointsObjective == true)
		{
			// TODO: Pending check the waypoints the player went through
		}

		// 5. Number of bombers shoot down by the Squad (depends on the mission type)
		if (waypointsObjective == true)
		{
			// TODO: Pending check the waypoints the player went through
		}

		// 6. ...Add more victor conditions
		if (bombersObjective == true)
		{
			// TODO: Pending check the waypoints the player went through
		}

		// Update the briefings and show the result
		if (missionCompleted == true)
		{
			Campaign.battleSuccess = true;
		}
		else
		{
			Campaign.battleSuccess = false;
		}

		// For testing purposes
		ShowResult();
		// For testing purposes
		ShowScore();
	}

	// Display score of the campaign
	private void ShowScore()
	{
		HudLog(String.Format("Campaign score: {0} air victories, destroyed {1} aircraft on ground, {2} ground targets, and {3} ships. ", cPlayerKillsScore, cPlayerStaticPlanesKillsScore, cPlayerGroundKillsScore, cPlayerShipKillsScore));
	}

	//Displays mission scores
	private void ShowResult()
	{
		//HudLog(String.Format("Our losses - {0} ", cFriendlyLosses) + String.Format(" Our Staffel losses - {0} ", cMySquadLosses));
		HudLog(String.Format("Enemy killed - {0} ", cEnemyAircraftDown) + String.Format(" Your victories - {0} ", cPlayerKills));
		if (cPlayerBomberKills > 0)
		{
			// Shows Destroyed bombers of a specific unit (Mission objective)
			HudLog(String.Format("You destroyed {0} bombers from the {1} RAF squadron", cPlayerBomberKills, missionObjectiveBombers));
		}

		// Shows ground victims
		if ((cPlayerGroundKills > 0) || (cPlayerStaticPlanesKills > 0))
		{
			HudLog(String.Format("You destroyed {0} ground Target(s), and {1} aircraft on ground", cPlayerGroundKills, cPlayerStaticPlanesKills));
		}

		// Shows ships sunk
		if (cEnemyShipDestroyed > 0)
		{
			HudLog(String.Format("British ships destroyed: {0}", cEnemyShipDestroyed));
		}
	}

	private void HudLog(String textToDisplay)
	{
		if (displayLogs)
		{
			Timeout(logsDuration, () =>
			{
				GamePlay.gpHUDLogCenter(textToDisplay);
			});
		}

	}
}
