// Copyright (c) 2026 Klemens Urban <klemens.urban@outlook.com>
// Based on XSchenFly by memo5@gmx.at (https://github.com/schenlap/XSchenFly)
// SPDX-License-Identifier: GPL-3.0-only

using nocscienceat.WinWingFcu.Hardware;

namespace nocscienceat.WinWingFcu.Panels;

public partial class FcuPanelHandler
{
    // =====================================================================
    // Dispatch table: maps HID button ID -> handler method.
    // Built once in CreateDispatchTable() after device detection.
    // Key = button ID from HID report, Value = (label, async handler(pressed))
    // =====================================================================
    private Dictionary<int, (string Label, Func<bool, Task> Handler)> _buttonHandlers = [];

    /// <summary>
    /// Registers all dataref paths and X-Plane command paths used by the FCU panel.
    /// Logical keys are mapped to actual X-Plane paths, enabling override via config.
    /// Called by the base class before <see cref="OnConnectedAsync"/>.
    /// </summary>
    protected override void RegisterDataRefsAndCommands()
    {
        // ===== Commands (fire-and-forget actions triggered by buttons/encoders) =====
        RegisterCommands(
        [
            // FCU pushbuttons
            ("MachToggle",      "toliss_airbus/ias_mach_button_push"),
            ("LocButton",       "AirbusFBW/LOCbutton"),
            ("HdgTrkToggle",    "toliss_airbus/hdgtrk_button_push"),
            ("AthrButton",      "AirbusFBW/ATHRbutton"),
            ("ExpedButton",     "AirbusFBW/EXPEDbutton"),
            ("MetricAlt",       "toliss_airbus/metric_alt_button_push"),
            ("ApprButton",      "AirbusFBW/APPRbutton"),

            // FCU encoders
            ("SpdDown",         "sim/autopilot/airspeed_down"),
            ("SpdUp",           "sim/autopilot/airspeed_up"),
            ("SpdPush",         "AirbusFBW/PushSPDSel"),
            ("SpdPull",         "AirbusFBW/PullSPDSel"),
            ("HdgDown",         "sim/autopilot/heading_down"),
            ("HdgUp",           "sim/autopilot/heading_up"),
            ("HdgPush",         "AirbusFBW/PushHDGSel"),
            ("HdgPull",         "AirbusFBW/PullHDGSel"),
            ("AltDown",         "sim/autopilot/altitude_down"),
            ("AltUp",           "sim/autopilot/altitude_up"),
            ("AltPush",         "AirbusFBW/PushAltitude"),
            ("AltPull",         "AirbusFBW/PullAltitude"),
            ("VsDown",          "sim/autopilot/vertical_speed_down"),
            ("VsUp",            "sim/autopilot/vertical_speed_up"),
            ("VsPush",          "AirbusFBW/PushVSSel"),
            ("VsPull",          "AirbusFBW/PullVSSel"),

            // EFIS-L buttons
            ("L_Fd",            "toliss_airbus/fd1_push"),
            ("L_Ls",            "toliss_airbus/dispcommands/CaptLSButtonPush"),
            ("L_Cstr",          "toliss_airbus/dispcommands/CaptCstrPushButton"),
            ("L_Wpt",           "toliss_airbus/dispcommands/CaptWptPushButton"),
            ("L_VorD",          "toliss_airbus/dispcommands/CaptVorDPushButton"),
            ("L_Ndb",           "toliss_airbus/dispcommands/CaptNdbPushButton"),
            ("L_Arpt",          "toliss_airbus/dispcommands/CaptArptPushButton"),
            ("L_BaroPush",      "toliss_airbus/capt_baro_push"),
            ("L_BaroPull",      "toliss_airbus/capt_baro_pull"),
            ("L_BaroDown",      "sim/instruments/barometer_down"),
            ("L_BaroUp",        "sim/instruments/barometer_up"),

            // EFIS-R buttons
            ("R_Fd",            "toliss_airbus/fd2_push"),
            ("R_Ls",            "toliss_airbus/dispcommands/CoLSButtonPush"),
            ("R_Cstr",          "toliss_airbus/dispcommands/CoCstrPushButton"),
            ("R_Wpt",           "toliss_airbus/dispcommands/CoWptPushButton"),
            ("R_VorD",          "toliss_airbus/dispcommands/CoVorDPushButton"),
            ("R_Ndb",           "toliss_airbus/dispcommands/CoNdbPushButton"),
            ("R_Arpt",          "toliss_airbus/dispcommands/CoArptPushButton"),
            ("R_BaroPush",      "toliss_airbus/copilot_baro_push"),
            ("R_BaroPull",      "toliss_airbus/copilot_baro_pull"),
            ("R_BaroDown",      "sim/instruments/barometer_copilot_down"),
            ("R_BaroUp",        "sim/instruments/barometer_copilot_up"),
        ]);

        // ===== DataRefs (subscribed values and write targets) =====
        RegisterDataRefs(
        [
            // FCU display values
            ("SpdDashed",       "AirbusFBW/SPDdashed"),
            ("HdgDashed",       "AirbusFBW/HDGdashed"),
            ("VsDashed",        "AirbusFBW/VSdashed"),
            ("AirspeedKtsMach", "sim/cockpit2/autopilot/airspeed_dial_kts_mach"),
            ("SpdManaged",      "AirbusFBW/SPDmanaged"),
            ("IsMach",          "sim/cockpit/autopilot/airspeed_is_mach"),
            ("HeadingMag",      "sim/cockpit/autopilot/heading_mag"),
            ("HdgManaged",      "AirbusFBW/HDGmanaged"),
            ("HdgTrkMode",      "AirbusFBW/HDGTRKmode"),
            ("Altitude",        "sim/cockpit/autopilot/altitude"),
            ("AltManaged",      "AirbusFBW/ALTmanaged"),
            ("VerticalVelocity","sim/cockpit/autopilot/vertical_velocity"),
            ("ApVerticalMode",  "AirbusFBW/APVerticalMode"),

            // FCU data buttons (write absolute values)
            ("Alt100_1000",     "AirbusFBW/ALT100_1000"),
            ("Ap1Engage",       "AirbusFBW/AP1Engage"),
            ("Ap2Engage",       "AirbusFBW/AP2Engage"),

            // FCU brightness (float 0..1 from X-Plane)
            ("Brightness",      "AirbusFBW/SupplLightLevelRehostats[0]"),
            ("BrightnessLcd",   "AirbusFBW/SupplLightLevelRehostats[1]"),

            // FCU LED status indicators
            ("ApprLed",         "AirbusFBW/APPRilluminated"),
            ("AthrLed",         "AirbusFBW/ATHRmode"),
            ("LocLed",          "AirbusFBW/LOCilluminated"),

            // EFIS-R baro
            ("BaroCopilot",     "sim/cockpit2/gauges/actuators/barometer_setting_in_hg_copilot"),
            ("BaroStdFO",       "AirbusFBW/BaroStdFO"),
            ("BaroUnitFO",      "AirbusFBW/BaroUnitFO"),

            // EFIS-L baro
            ("BaroPilot",       "sim/cockpit2/gauges/actuators/barometer_setting_in_hg_pilot"),
            ("BaroStdCapt",     "AirbusFBW/BaroStdCapt"),
            ("BaroUnitCapt",    "AirbusFBW/BaroUnitCapt"),

            // EFIS-L LED indicators and write targets
            ("L_ArptLed",       "AirbusFBW/NDShowARPTCapt"),
            ("L_NdbLed",        "AirbusFBW/NDShowNDBCapt"),
            ("L_VorDLed",       "AirbusFBW/NDShowVORDCapt"),
            ("L_WptLed",        "AirbusFBW/NDShowWPTCapt"),
            ("L_CstrLed",       "AirbusFBW/NDShowCSTRCapt"),
            ("L_FdLed",         "AirbusFBW/FD1Engage"),
            ("L_LsLed",         "AirbusFBW/ILSonCapt"),
            ("L_BaroUnit",      "AirbusFBW/BaroUnitCapt"),
            ("L_NdMode",        "AirbusFBW/NDmodeCapt"),
            ("L_NdRange",       "AirbusFBW/NDrangeCapt"),
            ("L_Efis1Sel",      "sim/cockpit2/EFIS/EFIS_1_selection_pilot"),
            ("L_Efis2Sel",      "sim/cockpit2/EFIS/EFIS_2_selection_pilot"),

            // EFIS-R LED indicators and write targets
            ("R_ArptLed",       "AirbusFBW/NDShowARPTFO"),
            ("R_NdbLed",        "AirbusFBW/NDShowNDBFO"),
            ("R_VorDLed",       "AirbusFBW/NDShowVORDFO"),
            ("R_WptLed",        "AirbusFBW/NDShowWPTFO"),
            ("R_CstrLed",       "AirbusFBW/NDShowCSTRFO"),
            ("R_FdLed",         "AirbusFBW/FD2Engage"),
            ("R_LsLed",         "AirbusFBW/ILSonFO"),
            ("R_BaroUnit",      "AirbusFBW/BaroUnitFO"),
            ("R_NdMode",        "AirbusFBW/NDmodeFO"),
            ("R_NdRange",       "AirbusFBW/NDrangeFO"),
            ("R_Efis1Sel",      "sim/cockpit2/EFIS/EFIS_1_selection_copilot"),
            ("R_Efis2Sel",      "sim/cockpit2/EFIS/EFIS_2_selection_copilot"),
        ]);
    }

    /// <summary>
    /// Builds the button dispatch table based on the detected device mask.
    /// Each HID button ID maps to a named handler with a human-readable label.
    /// Called once during <see cref="OnConnectedAsync"/> after device detection.
    /// </summary>
    private void CreateDispatchTable(DeviceMask deviceMask)
    {
        _buttonHandlers = new Dictionary<int, (string, Func<bool, Task>)>
        {
            // ===== FCU pushbuttons (IDs 0-8) =====
            [0] = ("MACH", p => FireCommand("MachToggle", p)),
            [1] = ("LOC", p => FireCommand("LocButton", p)),
            [2] = ("TRK", p => FireCommand("HdgTrkToggle", p)),
            [3] = ("AP1", p => FireToggleDataRef("Ap1Engage", p)),
            [4] = ("AP2", p => FireToggleDataRef("Ap2Engage", p)),
            [5] = ("A/THR", p => FireCommand("AthrButton", p)),
            [6] = ("EXPED", p => FireCommand("ExpedButton", p)),
            [7] = ("METRIC", p => FireCommand("MetricAlt", p)),
            [8] = ("APPR", p => FireCommand("ApprButton", p)),

            // ===== FCU encoders (IDs 9-24) =====
            [9] = ("SPD DEC", p => FireCommand("SpdDown", p)),
            [10] = ("SPD INC", p => FireCommand("SpdUp", p)),
            [11] = ("SPD PUSH", p => FireCommand("SpdPush", p)),
            [12] = ("SPD PULL", p => FireCommand("SpdPull", p)),
            [13] = ("HDG DEC", p => FireCommand("HdgDown", p)),
            [14] = ("HDG INC", p => FireCommand("HdgUp", p)),
            [15] = ("HDG PUSH", p => FireCommand("HdgPush", p)),
            [16] = ("HDG PULL", p => FireCommand("HdgPull", p)),
            [17] = ("ALT DEC", p => FireCommand("AltDown", p)),
            [18] = ("ALT INC", p => FireCommand("AltUp", p)),
            [19] = ("ALT PUSH", p => FireCommand("AltPush", p)),
            [20] = ("ALT PULL", p => FireCommand("AltPull", p)),
            [21] = ("VS DEC", p => FireCommand("VsDown", p)),
            [22] = ("VS INC", p => FireCommand("VsUp", p)),
            [23] = ("VS PUSH", p => FireCommand("VsPush", p)),
            [24] = ("VS PULL", p => FireCommand("VsPull", p)),

            // ===== FCU switches (IDs 25-26) =====
            [25] = ("ALT 100", p => FireSetDataRef("Alt100_1000", 0, p)),
            [26] = ("ALT 1000", p => FireSetDataRef("Alt100_1000", 1, p)),
        };

        // ===== EFIS-R buttons (IDs 32-61) =====
        if (deviceMask.HasFlag(DeviceMask.EfisR))
        {
            _buttonHandlers[32] = ("R_FD", p => FireCommand("R_Fd", p));
            _buttonHandlers[33] = ("R_LS", p => FireCommand("R_Ls", p));
            _buttonHandlers[34] = ("R_CSTR", p => FireCommand("R_Cstr", p));
            _buttonHandlers[35] = ("R_WPT", p => FireCommand("R_Wpt", p));
            _buttonHandlers[36] = ("R_VOR.D", p => FireCommand("R_VorD", p));
            _buttonHandlers[37] = ("R_NDB", p => FireCommand("R_Ndb", p));
            _buttonHandlers[38] = ("R_ARPT", p => FireCommand("R_Arpt", p));
            _buttonHandlers[39] = ("R_BARO PUSH", p => FireCommand("R_BaroPush", p));
            _buttonHandlers[40] = ("R_BARO PULL", p => FireCommand("R_BaroPull", p));
            _buttonHandlers[41] = ("R_BARO DEC", p => FireCommand("R_BaroDown", p));
            _buttonHandlers[42] = ("R_BARO INC", p => FireCommand("R_BaroUp", p));
            _buttonHandlers[43] = ("R_inHg", p => FireSetDataRef("R_BaroUnit", 0, p));
            _buttonHandlers[44] = ("R_hPa", p => FireSetDataRef("R_BaroUnit", 1, p));
            _buttonHandlers[45] = ("R_MODE LS", p => FireSetDataRef("R_NdMode", 0, p));
            _buttonHandlers[46] = ("R_MODE VOR", p => FireSetDataRef("R_NdMode", 1, p));
            _buttonHandlers[47] = ("R_MODE NAV", p => FireSetDataRef("R_NdMode", 2, p));
            _buttonHandlers[48] = ("R_MODE ARC", p => FireSetDataRef("R_NdMode", 3, p));
            _buttonHandlers[49] = ("R_MODE PLAN", p => FireSetDataRef("R_NdMode", 4, p));
            _buttonHandlers[50] = ("R_RANGE 10", p => FireSetDataRef("R_NdRange", 0, p));
            _buttonHandlers[51] = ("R_RANGE 20", p => FireSetDataRef("R_NdRange", 1, p));
            _buttonHandlers[52] = ("R_RANGE 40", p => FireSetDataRef("R_NdRange", 2, p));
            _buttonHandlers[53] = ("R_RANGE 80", p => FireSetDataRef("R_NdRange", 3, p));
            _buttonHandlers[54] = ("R_RANGE 160", p => FireSetDataRef("R_NdRange", 4, p));
            _buttonHandlers[55] = ("R_RANGE 320", p => FireSetDataRef("R_NdRange", 5, p));
            _buttonHandlers[56] = ("R_1 VOR", p => FireSetDataRef("R_Efis1Sel", 2, p));
            _buttonHandlers[57] = ("R_1 OFF", p => FireSetDataRef("R_Efis1Sel", 1, p));
            _buttonHandlers[58] = ("R_1 ADF", p => FireSetDataRef("R_Efis1Sel", 0, p));
            _buttonHandlers[59] = ("R_2 VOR", p => FireSetDataRef("R_Efis2Sel", 2, p));
            _buttonHandlers[60] = ("R_2 OFF", p => FireSetDataRef("R_Efis2Sel", 1, p));
            _buttonHandlers[61] = ("R_2 ADF", p => FireSetDataRef("R_Efis2Sel", 0, p));
        }

        // ===== EFIS-L buttons (IDs 64-93) =====
        if (deviceMask.HasFlag(DeviceMask.EfisL))
        {
            _buttonHandlers[64] = ("L_FD", p => FireCommand("L_Fd", p));
            _buttonHandlers[65] = ("L_LS", p => FireCommand("L_Ls", p));
            _buttonHandlers[66] = ("L_CSTR", p => FireCommand("L_Cstr", p));
            _buttonHandlers[67] = ("L_WPT", p => FireCommand("L_Wpt", p));
            _buttonHandlers[68] = ("L_VOR.D", p => FireCommand("L_VorD", p));
            _buttonHandlers[69] = ("L_NDB", p => FireCommand("L_Ndb", p));
            _buttonHandlers[70] = ("L_ARPT", p => FireCommand("L_Arpt", p));
            _buttonHandlers[71] = ("L_BARO PUSH", p => FireCommand("L_BaroPush", p));
            _buttonHandlers[72] = ("L_BARO PULL", p => FireCommand("L_BaroPull", p));
            _buttonHandlers[73] = ("L_BARO DEC", p => FireCommand("L_BaroDown", p));
            _buttonHandlers[74] = ("L_BARO INC", p => FireCommand("L_BaroUp", p));
            _buttonHandlers[75] = ("L_inHg", p => FireSetDataRef("L_BaroUnit", 0, p));
            _buttonHandlers[76] = ("L_hPa", p => FireSetDataRef("L_BaroUnit", 1, p));
            _buttonHandlers[77] = ("L_MODE LS", p => FireSetDataRef("L_NdMode", 0, p));
            _buttonHandlers[78] = ("L_MODE VOR", p => FireSetDataRef("L_NdMode", 1, p));
            _buttonHandlers[79] = ("L_MODE NAV", p => FireSetDataRef("L_NdMode", 2, p));
            _buttonHandlers[80] = ("L_MODE ARC", p => FireSetDataRef("L_NdMode", 3, p));
            _buttonHandlers[81] = ("L_MODE PLAN", p => FireSetDataRef("L_NdMode", 4, p));
            _buttonHandlers[82] = ("L_RANGE 10", p => FireSetDataRef("L_NdRange", 0, p));
            _buttonHandlers[83] = ("L_RANGE 20", p => FireSetDataRef("L_NdRange", 1, p));
            _buttonHandlers[84] = ("L_RANGE 40", p => FireSetDataRef("L_NdRange", 2, p));
            _buttonHandlers[85] = ("L_RANGE 80", p => FireSetDataRef("L_NdRange", 3, p));
            _buttonHandlers[86] = ("L_RANGE 160", p => FireSetDataRef("L_NdRange", 4, p));
            _buttonHandlers[87] = ("L_RANGE 320", p => FireSetDataRef("L_NdRange", 5, p));
            _buttonHandlers[88] = ("L_1 ADF", p => FireSetDataRef("L_Efis1Sel", 0, p));
            _buttonHandlers[89] = ("L_1 OFF", p => FireSetDataRef("L_Efis1Sel", 1, p));
            _buttonHandlers[90] = ("L_1 VOR", p => FireSetDataRef("L_Efis1Sel", 2, p));
            _buttonHandlers[91] = ("L_2 ADF", p => FireSetDataRef("L_Efis2Sel", 0, p));
            _buttonHandlers[92] = ("L_2 OFF", p => FireSetDataRef("L_Efis2Sel", 1, p));
            _buttonHandlers[93] = ("L_2 VOR", p => FireSetDataRef("L_Efis2Sel", 2, p));
        }
    }
}
