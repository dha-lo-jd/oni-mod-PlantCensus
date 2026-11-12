namespace PlantCensus;

// All user-visible text. Translations are loaded from translations/<language code>.po (see PlantCensusMod).
public static class STRINGS
{
    public static class UI
    {
        public static LocString TOOL_NAME = "Plant Census";
        public static LocString TOOL_TOOLTIP = "Drag to list the plants in an area";
        public static LocString TITLE = "Plant Census";
        public static LocString EMPTY = "No plants in the selected area.";

        public static class FILTERS
        {
            public static LocString STATE = "State";
            public static LocString SPECIES = "Species";
            public static LocString ALL = "All";
        }

        public static class STATES
        {
            public static LocString READY = "Ready";
            public static LocString GROWING = "Growing";
            public static LocString STALLED = "Stalled";
            public static LocString IDLE = "No growth";
        }

        public static class ORIGINS
        {
            public static LocString WILD = "Wild";
            public static LocString FARMED = "Farmed";
        }

        public static class HARVEST
        {
            public static LocString AUTO = "Auto";
            public static LocString MANUAL = "Manual";
        }

        public static class COLUMNS
        {
            public static LocString STATE = "State";
            public static LocString PLANT = "Plant";
            public static LocString ORIGIN = "Origin";
            public static LocString GROWTH = "Growth";
            public static LocString YIELD = "Yield";
            public static LocString HARVEST = "Harvest";
            public static LocString TEMPERATURE = "Temperature";
            public static LocString PROBLEMS = "Problems";
        }

        public static class FORMATS
        {
            // {0}: number of cycles
            public static LocString CYCLES = "{0}cyc";

            // {0}: a cycles value
            public static LocString LEFT = "({0} left)";
        }
    }
}
