namespace BaziBaqa
{
    /// <summary>
    /// نامِ گره‌های درخت صحنه (فقط برای Developer و lookup های transform.Find).
    /// این‌ها «متنِ بازی» نیستند و بازیکن آن‌ها را نمی‌بیند؛ بنابراین عمداً انگلیسیِ ثابت‌اند:
    /// نام‌های فارسی نباید به‌عنوان کلیدِ فنی استفاده شوند، چون با هر ویرایشِ متن، ارجاع
    /// <c>transform.Find</c> می‌شکند. برای متن‌های قابل‌مشاهده از LocalizationManager استفاده کنید.
    /// </summary>
    public static class WorldParts
    {
        // ریشه‌های جهان
        public const string WorldRoot = "World";
        public const string TerrainRoot = "Terrain";
        public const string ResourceRoot = "Resources";
        public const string ActorRoot = "Actors";
        public const string BuildingRoot = "Buildings";
        public const string EffectRoot = "Effects";

        // اجزای ساختمان
        public const string BuildingBody = "Body";
        public const string BuildingRoof = "Roof";
        public const string BuildingBeacon = "Beacon";
        public const string BuildingMast = "Mast";
        public const string BuildingCrop = "Crop";
        public const string BuildingChimney = "Chimney";

        // اجزای شخصیت‌ها
        public const string ActorTorso = "Torso";
        public const string ActorBackpack = "Backpack";
        public const string EnemyEye = "ThreatMarker";

        // عناصر طبیعت
        public const string Ground = "IslandGround";
        public const string GroundMesh = "ProceduralIsland";
        public const string Water = "ShallowWater";
        public const string Tree = "Tree";
        public const string TreeTrunk = "Trunk";
        public const string TreeCanopy = "Canopy";
        public const string Berry = "Berry";
        public const string Rock = "SmallRock";
        public const string SunLight = "Sun";
        public const string ResourceNode = "ResourceNode_";
        public const string Enemy = "Enemy_";
        public const string Bird = "Bird_";
        public const string Label = "Label";
        public const string BuildGhost = "BuildGhost";
        public const string Rain = "Rain";

        // اجزای جلوه‌ها
        public const string Fire = "Fire";
        public const string Smoke = "Smoke";
        public const string Spark = "Spark";
        public const string WaterShimmer = "WaterShimmer";
        public const string EnergyParticle = "EnergyDust";
        public const string Lightning = "LightningFlash";

        // سامانه‌ها
        public const string InputEventSystem = "Input EventSystem";
        // ---- گره‌های لایه‌ی گرافیک (فاز ۳)؛ همه ASCII تا قواعدِ نام‌گذاریِ صحنه نشکند ----
        public const string GraphicsDirector = "GraphicsDirector";
        public const string LightingRig = "CinematicLightingRig";
        public const string EnvironmentFx = "EnvironmentFx";
        public const string QualityDirector = "QualityDirector";

        public const string GameCamera = "Game Camera";
        public const string UiCanvas = "UI Canvas";
    }
}
