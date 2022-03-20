using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using KModkit;

public class SorrySliders : MonoBehaviour {
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;

    public KMSelectable[] BoardSpaces;
    public KMSelectable PowerButton;

    public MeshRenderer[] PowerBars;
    public MeshRenderer[] Arrows;
    public MeshRenderer[] Rings;

    public Material[] PowerBarColors;
    public Material[] ArrowLightColors;
    public Material[] RingColors;

    public Transform[] Pawns;
    public MeshRenderer ShootingPawn;

    // Solving info
    private int[] pawnPositions = new int[3];
    private DateTime date;
    private float startTime;

    private int surface;
    private bool fixedSurface;
    private readonly int fixedSurfaceVal = 27;
    private int travelDistance;

    private bool validPress;
    private bool canPress = true;
    private int shot;
    private int landedVal;

    private bool arrowsLocked;
    private float randomVal = 0.2f;
    private int currentArrow;

    private bool setPower;
    private bool powerLocked = true;
    private int currentPower = 1;

    private static readonly int[] centerTable = { 0, 0, 1, 1, 2, 2, 3, 3, 5, 5, 3, 3, 2, 2, 1, 1 };
    private static readonly int[] edgeTable = { 0, 0, 0, 1, 1, 2, 2, 3, 3, 3, 3, 2, 2, 1, 1, 0 };

    private static readonly float[] spaceXCoords = { -0.0447f, -0.0472f, -0.0398f, -0.0364f, -0.0446f, -0.0521f, -0.0447f };
    private static readonly float[] spaceZCoords = { -0.0635f, -0.0435f, -0.0235f, -0.0035f, 0.0165f, 0.0365f, 0.0615f };

    private static readonly float[] shootingXCoords = { 0.0234f, 0.0364f, 0.0494f };
    private static readonly float[] shootingZCoordsC = { -0.0225f, -0.0225f, -0.01f, -0.01f, -0.00175f, -0.00175f, 0.00675f, 0.00675f, 0.019f, 0.019f, 0.03125f, 0.03125f, 0.03975f, 0.03975f, 0.048f, 0.048f };
    private static readonly float[] shootingZCoordsE = { -0.0225f, -0.0225f, -0.0225f, -0.007f, -0.007f, 0.003f, 0.003f, 0.019f, 0.019f, 0.019f, 0.019f, 0.035f, 0.035f, 0.045f, 0.045f, 0.055f };

    // Bomb info
    private string serialNumber;
    private int moduleCount;
    private bool hasYahtzee;

    // Logging info
    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved = false;

    // Ran as bomb loads
    private void Awake() {
        moduleId = moduleIdCounter++;

        PowerButton.OnInteract += delegate () { PowerButtonPress(); return false; };
        for (int i = 0; i < BoardSpaces.Length; i++) {
            int j = i + 1;
            BoardSpaces[i].OnInteract += delegate () { BoardSpacePress(j); return false; };
        }
	}

    // Sets information
    private void Start() {
        Debug.LogFormat("[Sorry Sliders #{0}] Welcome to Sorry Sliders!", moduleId);

        for (int i = 0; i < pawnPositions.Length; i++)
            pawnPositions[i] = 0;

        date = DateTime.Now;
        startTime = Bomb.GetTime();

        ShootingPawn.enabled = false;

        serialNumber = Bomb.GetSerialNumber();
        moduleCount = Bomb.GetModuleNames().Count();
        hasYahtzee = Bomb.GetModuleNames().Contains("Yahtzee");

        int[] surfaceValues = { 24, 25, 26, 27, 28, 29, 35, 36, 37 }; // Temporary

        if (fixedSurface)
            surface = fixedSurfaceVal;
        else
            surface = surfaceValues[UnityEngine.Random.Range(0, surfaceValues.Length)]; // Fix the equation at some point
            //surface = UnityEngine.Random.Range(23, 38);

        Debug.LogFormat("<Sorry Sliders #{0}> Surface value: {1}", moduleId, surface);

        randomVal = UnityEngine.Random.Range(0.15f, 0.3f);
        StartCoroutine(StartArrowFlash());
    }


    // Power button pressed
    private void PowerButtonPress() {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        PowerButton.AddInteractionPunch(0.4f);

        if (canPress) {
            validPress = false;

            // Set direction
            if (!setPower) {
                Audio.PlaySoundAtTransform("SorrySliders_Button", transform);
                arrowsLocked = true;
                if (currentArrow == 3)
                    currentArrow = 1;

                setPower = true;
                powerLocked = false;
                currentPower = UnityEngine.Random.Range(0, 8);
                StartCoroutine(StartPowerFlash());
            }

            // Set power
            else {
                powerLocked = true;
                setPower = false;
                canPress = false;
                /* 0 = 0
                 * 1 = 1
                 * 2 = 2
                 * 3 = 3
                 * 4 = 4
                 * 5 = 3
                 * 6 = 2
                 * 7 = 1
                 */
                switch (currentPower) {
                    case 5: currentPower = 3; break;
                    case 6: currentPower = 2; break;
                    case 7: currentPower = 1; break;
                }

                StartCoroutine(LaunchPawn());
            }
        }
    }

    // Board space pressed
    private void BoardSpacePress(int i) {
        BoardSpaces[i - 1].AddInteractionPunch(0.4f);

        if (!moduleSolved && !validPress) {
            Debug.LogFormat("[Sorry Sliders #{0}] You tried to move a pawn out of turn! Strike!", moduleId);
            StartCoroutine(Strike());
        }

        else if (!moduleSolved && validPress) {
            int correctDistance = GetCorrectDistance(landedVal);

            Debug.LogFormat("[Sorry Sliders #{0}] You moved a pawn. Expected distance: {1}", moduleId, correctDistance);

            // Checks to see if the distance moved is valid
            bool valid = false;

            if (i - pawnPositions[0] == correctDistance) {
                valid = true;
                pawnPositions[0] += correctDistance;
                Pawns[0].localPosition = new Vector3(spaceXCoords[pawnPositions[0]] - 0.01f, 0.0152f, spaceZCoords[pawnPositions[0]]);
            }

            else if (i - pawnPositions[1] == correctDistance) {
                valid = true;
                pawnPositions[1] += correctDistance;
                Pawns[1].localPosition = new Vector3(spaceXCoords[pawnPositions[1]], 0.0152f, spaceZCoords[pawnPositions[1]]);
            }

            else if (i - pawnPositions[2] == correctDistance) {
                valid = true;
                pawnPositions[2] += correctDistance;
                Pawns[2].localPosition = new Vector3(spaceXCoords[pawnPositions[2]] + 0.01f, 0.0152f, spaceZCoords[pawnPositions[2]]);
            }

            if (valid) {
                Audio.PlaySoundAtTransform("SorrySliders_Space", transform);
                Debug.LogFormat("[Sorry Sliders #{0}] Pawn moved successfully!", moduleId);
                Debug.LogFormat("[Sorry Sliders #{0}] Current pawn positions: {1}, {2}, {3}", moduleId, pawnPositions[0], pawnPositions[1], pawnPositions[2]);
                validPress = false;

                // Module solves
                if (pawnPositions[0] >= 6 && pawnPositions[1] >= 6 && pawnPositions[2] >= 6) {
                    canPress = false;
                    moduleSolved = true;
                    StartCoroutine(Solve());
                }
            }

            else {
                Debug.LogFormat("[Sorry Sliders #{0}] Pawn moved incorrectly - try again! Strike!", moduleId);
                StartCoroutine(Strike());
            }
        }
    }


    // Calculates the correct distance
    private int GetCorrectDistance(int val) {
        // Checks if all the pawns are at most one space away from home
        if (pawnPositions[0] >= 5 && pawnPositions[1] >= 5 && pawnPositions[2] >= 5)
            return 1;

        switch (val) {
        case 1:
            if (shot == 4) return 1;
            else if (date.DayOfWeek == DayOfWeek.Monday) return 2;
            else if (hasYahtzee) return 1;
            else if (Bomb.GetTime() <= startTime / 2.0f) return 2;
            else return 1;

        case 2:
            if (shot == 3) return 2;
            else if (date.DayOfWeek == DayOfWeek.Friday) return 3;
            else if (date.Hour >= 12) return 1;
            else if (serialNumber.Any(x => x == 'T')) return 3;
            else return 2;

        case 3:
            if (shot == 2) return 3;
            else if (date.DayOfWeek == DayOfWeek.Sunday) return 1;
            else if (serialNumber.Any(x => x == 'A' || x == 'E' || x == 'I' || x == 'O' || x == 'U')) return 2;
            else if (serialNumber.Any(x => x == 'S')) return 5;
            else return 3;

        case 5:
            if (shot == 1) return 5;
            else if (date.DayOfWeek == DayOfWeek.Wednesday) return 3;
            else if (Bomb.GetTime() > startTime / 2.0f) return 2;
            else if (moduleCount > 10) return 3;
            else return 5;

        default:
            return 1;
        }
    }

    // Module strikes
    private IEnumerator Strike() {
        GetComponent<KMBombModule>().HandleStrike();
        Audio.PlaySoundAtTransform("SorrySliders_Strike", transform);
        yield return new WaitForSeconds(0.4f);
        Audio.PlaySoundAtTransform("SorrySliders_SORRY", transform);
    }

    // Module solves
    private IEnumerator Solve() {
        yield return new WaitForSeconds(2.0f);
        Audio.PlaySoundAtTransform("SorrySliders_Solve", transform);
        Debug.LogFormat("[Sorry Sliders #{0}] All the pawns have reached Home! Module solved!", moduleId);
        GetComponent<KMBombModule>().HandlePass();

        arrowsLocked = true;
        yield return new WaitForSeconds(randomVal);
        Arrows[0].material = ArrowLightColors[0];
        Arrows[1].material = ArrowLightColors[0];
        Arrows[2].material = ArrowLightColors[0];
    }


    // Arrow lights flash
    private IEnumerator StartArrowFlash() {
        if (!arrowsLocked) {
            currentArrow++;
            currentArrow %= 4;

            switch (currentArrow) {
            case 0:
                Arrows[0].material = ArrowLightColors[1];
                Arrows[1].material = ArrowLightColors[0];
                Arrows[2].material = ArrowLightColors[0];
            break;

            case 2:
                Arrows[0].material = ArrowLightColors[0];
                Arrows[1].material = ArrowLightColors[0];
                Arrows[2].material = ArrowLightColors[1];
            break;

            default:
                Arrows[0].material = ArrowLightColors[0];
                Arrows[1].material = ArrowLightColors[1];
                Arrows[2].material = ArrowLightColors[0];
            break;
            }

            yield return new WaitForSeconds(randomVal);
            StartCoroutine(StartArrowFlash());
        }
    }

    // Power lights flash
    private IEnumerator StartPowerFlash() {
        if (!powerLocked) {
            switch (currentPower) {
            case 0:
                currentPower++;
                PowerBars[0].material = PowerBarColors[1];
                PowerBars[1].material = PowerBarColors[2];
                PowerBars[2].material = PowerBarColors[4];
                PowerBars[3].material = PowerBarColors[6];
            break;

            case 1:
                currentPower++;
                PowerBars[0].material = PowerBarColors[1];
                PowerBars[1].material = PowerBarColors[3];
                PowerBars[2].material = PowerBarColors[4];
                PowerBars[3].material = PowerBarColors[6];
            break;

            case 2:
                currentPower++;
                PowerBars[0].material = PowerBarColors[1];
                PowerBars[1].material = PowerBarColors[3];
                PowerBars[2].material = PowerBarColors[5];
                PowerBars[3].material = PowerBarColors[6];
            break;

            case 3:
                currentPower++;
                PowerBars[0].material = PowerBarColors[1];
                PowerBars[1].material = PowerBarColors[3];
                PowerBars[2].material = PowerBarColors[5];
                PowerBars[3].material = PowerBarColors[7];
            break;

            case 4:
                currentPower++;
                PowerBars[0].material = PowerBarColors[1];
                PowerBars[1].material = PowerBarColors[3];
                PowerBars[2].material = PowerBarColors[5];
                PowerBars[3].material = PowerBarColors[6];
            break;

            case 5:
                currentPower++;
                PowerBars[0].material = PowerBarColors[1];
                PowerBars[1].material = PowerBarColors[3];
                PowerBars[2].material = PowerBarColors[4];
                PowerBars[3].material = PowerBarColors[6];
            break;

            case 6:
                currentPower++;
                PowerBars[0].material = PowerBarColors[1];
                PowerBars[1].material = PowerBarColors[2];
                PowerBars[2].material = PowerBarColors[4];
                PowerBars[3].material = PowerBarColors[6];
            break;

            default:
                currentPower = 0;
                PowerBars[0].material = PowerBarColors[0];
                PowerBars[1].material = PowerBarColors[2];
                PowerBars[2].material = PowerBarColors[4];
                PowerBars[3].material = PowerBarColors[6];
            break;
            }

            yield return new WaitForSeconds(0.15f);
            StartCoroutine(StartPowerFlash());
        }
    }

    // Pawn gets launched
    private IEnumerator LaunchPawn() {
        Audio.PlaySoundAtTransform("SorrySliders_Shoot", transform);
        shot++;

        ShootingPawn.enabled = true;
        Pawns[3].localPosition = new Vector3(shootingXCoords[currentArrow], 0.0152f, -0.05f);

        // Determines the distination
        float distance = surface * currentPower / 10.0f + 1.0f;
        travelDistance = (int) Math.Floor(distance);

        if (currentArrow == 1)
            landedVal = centerTable[travelDistance];

        else
            landedVal = edgeTable[travelDistance];

        int flashingRing = landedVal;

        if (landedVal == 5)
            flashingRing = 4;


        // Animates the pawn
        float dist = 0.0f;

        if (currentArrow == 1)
            dist = shootingZCoordsC[travelDistance] + 0.05f;

        else
            dist = shootingZCoordsE[travelDistance] + 0.05f;

        for (int i = 0; i < 50; i++) {
            Pawns[3].localPosition = new Vector3(shootingXCoords[currentArrow], 0.0152f, -0.05f + 0.5f * dist * (float) Math.Log10(2 * i + 1));
            yield return new WaitForSeconds(0.02f);
        }

        Pawns[3].localPosition = new Vector3(shootingXCoords[currentArrow], 0.0152f, dist - 0.05f);

        // Animation finishes
        validPress = landedVal != 0;

        if (flashingRing > 0) {
            Audio.PlaySoundAtTransform("SorrySliders_Land", transform);
            Debug.LogFormat("[Sorry Sliders #{0}] Your shot landed on: {1}.", moduleId, landedVal);
            Debug.LogFormat("[Sorry Sliders #{0}] The correct distance is {1}.", moduleId, GetCorrectDistance(landedVal));


            Rings[flashingRing - 1].material = RingColors[4];
            yield return new WaitForSeconds(0.15f);
            Rings[flashingRing - 1].material = RingColors[flashingRing - 1];
            yield return new WaitForSeconds(0.15f);
            Rings[flashingRing - 1].material = RingColors[4];
            yield return new WaitForSeconds(0.15f);
            Rings[flashingRing - 1].material = RingColors[flashingRing - 1];
            yield return new WaitForSeconds(0.15f);
            Rings[flashingRing - 1].material = RingColors[4];
            yield return new WaitForSeconds(0.15f);
            Rings[flashingRing - 1].material = RingColors[flashingRing - 1];
            yield return new WaitForSeconds(0.15f);
        }

        else
            Debug.LogFormat("[Sorry Sliders #{0}] Your shot missed the board!", moduleId);

        arrowsLocked = false;
        canPress = true;
        powerLocked = false;

        PowerBars[0].material = PowerBarColors[0];
        PowerBars[1].material = PowerBarColors[2];
        PowerBars[2].material = PowerBarColors[4];
        PowerBars[3].material = PowerBarColors[6];

        randomVal = UnityEngine.Random.Range(0.1f, 0.25f);
        StartCoroutine(StartArrowFlash());
    }


    // Twitch Plays - Thanks to Danny7007


#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use <!{0} aim left/middle/right> to aim the arrow in that position. Use <!{0} power 1-4> to set that power level (from bottom to top). Use <!{0} press 1-6> to press that space from bottom to top. Space 6 is the home space.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToUpperInvariant();
        Match[] m = new Match[] { Regex.Match(command, @"^AIM\s+(LEFT|MIDDLE|RIGHT)$"), Regex.Match(command, @"^POWER\s+([1-4])$"), Regex.Match(command, @"^PRESS\s+([1-6])$") };
        if (m[0].Success) //Aim
        {
            if (arrowsLocked)
                yield return "sendtochaterror The aim position is already set.";
            else
            {
                yield return null;
                while (currentArrow != "LMR".IndexOf(m[0].Groups[1].Value[0]))
                    yield return null;
                PowerButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
        else if (m[1].Success)
        {
            if (!arrowsLocked)
                yield return "sendtochaterror You must first set the arrow positions before you set the power.";
            else if (!canPress)
                yield return "sendtochaterror You cannot press the button at this time.";
            else
            {
                yield return null;
                while (currentPower != m[1].Groups[1].Value[0] - '0')
                    yield return null;
                PowerButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
        else if (m[2].Success)
        {
            if (!validPress)
                yield return "sendtochaterror You cannot move to a space at this time.";
            else
            {
                yield return null;
                BoardSpaces[m[2].Groups[1].Value[0] - '1'].OnInteract();
                yield return new WaitForSeconds(0.1f);
                if (pawnPositions.All(x => x == 6))
                    yield return "solve";
            }
        }
        yield return null;
    }
}