using System.Globalization;
using RunawayExplorer.Core.FileSystem;
using RunawayExplorer.Core.Formats;

namespace RunawayExplorer.Core.Metadata;

/// <summary>
/// Human-readable names and official chapter information for Runaway's assets.
/// Provides bilingual support (English and Spanish, the game's original development language).
/// </summary>
public static class SceneCatalog
{
    public const string DefaultLanguage = "en";

    public record ChapterInfo(int Number, string TitleEn, string TitleEs);

    public record SceneInfo(string ArchiveName, int Chapter, string NameEn, string NameEs);

    public static readonly IReadOnlyList<ChapterInfo> Chapters =
    [
        new(1, "Wake Me Before Dying", "Despiértame antes de morir"),
        new(2, "The Mysterious Crucifix", "El extraño crucifijo"),
        new(3, "The Great Escape", "La gran evasión"),
        new(4, "Close Encounters of the Fourth Kind", "Encuentros en la cuarta fase"),
        new(5, "Gifts from the Crypt", "La cripta sagrada"),
        new(6, "The Indian, the Nun and the Finger", "El indio, la monja y el dedo"),
    ];

    public static readonly IReadOnlyList<ChapterInfo> ChaptersR1 = Chapters;

    public static readonly IReadOnlyList<ChapterInfo> ChaptersR2 =
    [
        new(1, "Mala Island", "La isla de Mala"),
        new(2, "Surfin' Mala", "Surfeando en Mala"),
        new(3, "Simpler than an Amoeba", "Más simple que una ameba"),
        new(4, "The Guy Who Can Speak to Platypuses", "El hombre que habla con los ornitorrincos"),
        new(5, "Allerton Court", "Allerton Court"),
        new(6, "The Hidden Temple", "El templo oculto"),
    ];

    public static readonly IReadOnlyList<ChapterInfo> ChaptersR3 =
    [
        new(1, "Brian Basco Is Dead", "Brian Basco ha muerto"),
        new(2, "The Art of Running Away", "El arte de escapar"),
        new(3, "A Timely Suicide", "Un suicidio bien oportuno"),
        new(4, "An Unexpected Ally", "Un aliado inesperado"),
        new(5, "Deconstructing Brian", "Brian en todos sus estados"),
        new(6, "The End Is Here", "El final está cerca"),
        new(7, "Epilogue & Flashbacks", "Epílogo y recuerdos"),
    ];

    private static readonly Dictionary<int, ChapterInfo> ChaptersByNumber =
        Chapters.ToDictionary(c => c.Number);

    private static readonly Dictionary<int, ChapterInfo> ChaptersByNumberR2 =
        ChaptersR2.ToDictionary(c => c.Number);

    private static readonly Dictionary<int, ChapterInfo> ChaptersByNumberR3 =
        ChaptersR3.ToDictionary(c => c.Number);

    private static readonly Dictionary<string, SceneInfo> ScenesByName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Intro, Main Menu & Developer Logos
            ["RESOURCE.A00"] = new("RESOURCE.A00", 1, "Hospital: Gina's Room (Intro)", "Hospital: Habitación de Gina (Inicio)"),
            ["RESOURCE.A01"] = new("RESOURCE.A01", 1, "Hospital: Gina's Room & Chapter Select", "Hospital: Habitación de Gina y selección de capítulo"),
            ["RESOURCE.A02"] = new("RESOURCE.A02", 0, "Developer Logos: Pendulo Studios & FX", "Logotipos: Pendulo Studios y FX"),
            ["RESOURCE.A03"] = new("RESOURCE.A03", 0, "Main Menu: Chapter Selection Thumbnails", "Menú principal: Miniaturas de selección de capítulo"),

            // Chapter 1: Wake Me Before Dying / Despiértame antes de morir (Hospital)
            ["RESOURCE.B01"] = new("RESOURCE.B01", 1, "Suburbs at Night: Brian's Departure", "Suburbios de noche: Partida de Brian"),
            ["RESOURCE.B02"] = new("RESOURCE.B02", 1, "Hospital: Gina's Room", "Hospital: Habitación de Gina"),
            ["RESOURCE.B06"] = new("RESOURCE.B06", 1, "Hospital: Storage Room & Medical Archives", "Hospital: Almacén y archivo médico"),
            ["RESOURCE.B08"] = new("RESOURCE.B08", 1, "Hospital: Gina's Room Bathroom", "Hospital: Baño de la habitación de Gina"),

            // Chapter 2: The Mysterious Crucifix / El extraño crucifijo (Chicago Museum of Natural History)
            ["RESOURCE.D03"] = new("RESOURCE.D03", 2, "Museum: Clive's Office", "Museo: Despacho de Clive"),
            ["RESOURCE.D05"] = new("RESOURCE.D05", 2, "Museum: Restoration Lab", "Museo: Laboratorio de restauración"),
            ["RESOURCE.D06"] = new("RESOURCE.D06", 2, "Museum: Main Lobby (Dinosaur Skeletons)", "Museo: Vestíbulo principal (Esqueletos de dinosaurios)"),
            ["RESOURCE.D07"] = new("RESOURCE.D07", 2, "Museum: Triceratops Display & Janitor Area", "Museo: Expositor del triceratops y rincón de limpieza"),
            ["RESOURCE.D09"] = new("RESOURCE.D09", 2, "Museum: Mayan Exhibition Hall", "Museo: Sala de exposiciones mayas"),
            ["RESOURCE.D10"] = new("RESOURCE.D10", 2, "Museum: Thermal Analysis Lab", "Museo: Laboratorio de análisis térmico"),
            ["RESOURCE.D14"] = new("RESOURCE.D14", 2, "Museum: ACME Security Keypad", "Museo: Teclado de seguridad ACME"),

            // Chapter 3: The Great Escape / La gran evasión (Desert & Thugs' Cabin)
            ["RESOURCE.E01"] = new("RESOURCE.E01", 3, "Desert: Region Map & Overview", "Desierto: Mapa de la región y vista general"),
            ["RESOURCE.E02"] = new("RESOURCE.E02", 3, "Desert: Thugs' Cabin (Front Exterior)", "Desierto: Cabaña de los matones (Exterior frontal)"),
            ["RESOURCE.E03"] = new("RESOURCE.E03", 3, "Desert: Thugs' Cabin (Main Room)", "Desierto: Cabaña de los matones (Sala principal)"),
            ["RESOURCE.E04"] = new("RESOURCE.E04", 3, "Desert: Thugs' Cabin (Closet View)", "Desierto: Cabaña de los matones (Vista desde el armario)"),
            ["RESOURCE.E05"] = new("RESOURCE.E05", 3, "Desert: Aircraft Graveyard (Fighter Plane)", "Desierto: Cementerio de aviones (Caza militar)"),
            ["Resource.e06"] = new("Resource.e06", 3, "Desert: Drag Queens' Bus & Camp (Exterior)", "Desierto: Autobús y campamento de las drag queens (Exterior)"),
            ["RESOURCE.E07"] = new("RESOURCE.E07", 3, "Desert: Oil Well & Barrels", "Desierto: Pozo de petróleo y barriles"),
            ["RESOURCE.E08"] = new("RESOURCE.E08", 3, "Desert: Abandoned Train Station & Boxcar", "Desierto: Estación abandonada y vagón de tren"),
            ["RESOURCE.E10"] = new("RESOURCE.E10", 3, "Desert: Thugs' Cabin (Back Scrap Yard)", "Desierto: Cabaña de los matones (Chatarrería trasera)"),
            ["RESOURCE.E11"] = new("RESOURCE.E11", 3, "Desert: Abandoned Train Boxcar (Interior)", "Desierto: Vagón de tren abandonado (Interior)"),
            ["RESOURCE.E13"] = new("RESOURCE.E13", 3, "Desert: Abandoned Hangar (Sandretto Helicopter)", "Desierto: Hangar abandonado (Helicóptero Sandretto)"),
            ["RESOURCE.E14"] = new("RESOURCE.E14", 3, "Desert: Canyon Trail & Telephone Pole", "Desierto: Sendero del cañón y poste de teléfono"),
            ["RESOURCE.E16"] = new("RESOURCE.E16", 3, "Desert: Drag Queens' Bus (Interior, Rear View)", "Desierto: Autobús de las drag queens (Interior, parte trasera)"),
            ["RESOURCE.E17"] = new("RESOURCE.E17", 3, "Desert: Drag Queens' Bus (Interior, Driver View)", "Desierto: Autobús de las drag queens (Interior, puesto de conducción)"),

            // Chapter 4: Close Encounters of the Fourth Kind / Encuentros en la cuarta fase (Douglasville)
            ["RESOURCE.F03"] = new("RESOURCE.F03", 4, "Douglasville: Abandoned Mine Tunnel (Inside)", "Douglasville: Túnel de la mina abandonada (Interior)"),
            ["RESOURCE.F08"] = new("RESOURCE.F08", 4, "Douglasville: Abandoned Mine Entrance", "Douglasville: Entrada a la mina abandonada"),
            ["RESOURCE.F09"] = new("RESOURCE.F09", 4, "Douglasville: Mamá Dorita's House (Front Porch)", "Douglasville: Casa de Mamá Dorita (Porche)"),
            ["RESOURCE.F13"] = new("RESOURCE.F13", 4, "Douglasville: Mamá Dorita's House & Water Well", "Douglasville: Casa de Mamá Dorita y pozo de agua"),
            ["RESOURCE.F18"] = new("RESOURCE.F18", 4, "Douglasville: Main Street", "Douglasville: Calle principal"),
            ["RESOURCE.F19"] = new("RESOURCE.F19", 4, "Douglasville: Ruined Bank", "Douglasville: Banco en ruinas"),
            ["RESOURCE.F20"] = new("RESOURCE.F20", 4, "Douglasville: Sheriff's Office & Jail", "Douglasville: Oficina del sheriff y cárcel"),
            ["RESOURCE.F21"] = new("RESOURCE.F21", 4, "Douglasville: Jail Cell Close-up (Skeleton on Cot)", "Douglasville: Celda de la cárcel (Esqueleto en el catre)"),
            ["RESOURCE.F22"] = new("RESOURCE.F22", 4, "Douglasville: Saloon Balcony Overlooking Main Street", "Douglasville: Balcón del saloon hacia la calle principal"),
            ["RESOURCE.F24"] = new("RESOURCE.F24", 4, "Douglasville: Saloon (Rutger's Greenhouse Corner)", "Douglasville: Saloon (Rincón invernadero de Rutger)"),
            ["RESOURCE.F25"] = new("RESOURCE.F25", 4, "Douglasville: Saloon (Collapsed Cellar & Sculpture)", "Douglasville: Saloon (Sótano derrumbado y escultura)"),
            ["RESOURCE.F26"] = new("RESOURCE.F26", 4, "Douglasville: Derailed Steam Locomotive", "Douglasville: Locomotora de vapor descarrilada"),
            ["RESOURCE.F27"] = new("RESOURCE.F27", 4, "Douglasville: Meteor Crater (Panoramic Overlook)", "Douglasville: Cráter del meteorito (Vista panorámica)"),
            ["RESOURCE.F28"] = new("RESOURCE.F28", 4, "Douglasville: Meteor Crater (Crater Floor)", "Douglasville: Cráter del meteorito (Fondo del cráter)"),
            ["RESOURCE.F29"] = new("RESOURCE.F29", 4, "Douglasville: Joshua's Crater Camp & Synthesizer", "Douglasville: Campamento de Joshua y sintetizador"),
            ["RESOURCE.F30"] = new("RESOURCE.F30", 4, "Douglasville: Saturn's Sculptor Studio & Balcony", "Douglasville: Taller de escultura de Saturno y balcón"),
            ["RESOURCE.F35"] = new("RESOURCE.F35", 4, "Douglasville: Region Map & Overview", "Douglasville: Mapa de la región y vista general"),
            ["RESOURCE.F37"] = new("RESOURCE.F37", 4, "Douglasville: Hotel Lobby & Pool Table", "Douglasville: Vestíbulo del hotel y mesa de billar"),
            ["RESOURCE.F39"] = new("RESOURCE.F39", 4, "Douglasville: Bank Safe Combination Lock Dial", "Douglasville: Dial de combinación de la caja fuerte del banco"),

            // Chapter 5: Gifts from the Crypt & Chapter 6: The Indian, the Nun and the Finger (Douglasville Military Occupation)
            ["RESOURCE.H09"] = new("RESOURCE.H09", 5, "Douglasville: Mamá Dorita's House (Chapter 5/6)", "Douglasville: Casa de Mamá Dorita (Capítulo 5/6)"),
            ["RESOURCE.H10"] = new("RESOURCE.H10", 5, "Douglasville: Mamá Dorita's Hotel Room", "Douglasville: Habitación de hotel de Mamá Dorita"),
            ["Resource.h13"] = new("Resource.h13", 5, "Douglasville: Mamá Dorita's House & Water Well (Chapter 5/6)", "Douglasville: Casa de Mamá Dorita y pozo (Capítulo 5/6)"),
            ["Resource.h18"] = new("Resource.h18", 6, "Military Outpost: Douglasville Main Street (Roadblock)", "Puesto militar: Calle principal de Douglasville (Control)"),
            ["RESOURCE.H19"] = new("RESOURCE.H19", 6, "Military Outpost: Douglasville Bank", "Puesto militar: Banco de Douglasville"),
            ["RESOURCE.H20"] = new("RESOURCE.H20", 6, "Military Outpost: Sheriff's Office & Jail", "Puesto militar: Oficina del sheriff y cárcel"),
            ["RESOURCE.H21"] = new("RESOURCE.H21", 6, "Military Outpost: Jail Cell Close-up", "Puesto militar: Celda de la cárcel"),
            ["RESOURCE.H22"] = new("RESOURCE.H22", 6, "Military Outpost: Saloon Balcony", "Puesto militar: Balcón del saloon"),
            ["Resource.h23"] = new("Resource.h23", 4, "Douglasville: Saloon Piano Mechanism", "Douglasville: Mecanismo del piano del saloon"),
            ["RESOURCE.H24"] = new("RESOURCE.H24", 6, "Military Outpost: Saloon (Rutger's Spot)", "Puesto militar: Saloon (Rincón de Rutger)"),
            ["RESOURCE.H26"] = new("RESOURCE.H26", 6, "Military Outpost: Derailed Locomotive", "Puesto militar: Locomotora descarrilada"),
            ["RESOURCE.H29"] = new("RESOURCE.H29", 6, "Military Outpost: Joshua's Crater Camp", "Puesto militar: Campamento del cráter de Joshua"),
            ["RESOURCE.H30"] = new("RESOURCE.H30", 6, "Military Outpost: Saturn's Studio", "Puesto militar: Taller de Saturno"),
            ["RESOURCE.H35"] = new("RESOURCE.H35", 6, "Military Outpost: Douglasville Map", "Puesto militar: Mapa de Douglasville"),
            ["RESOURCE.H37"] = new("RESOURCE.H37", 6, "Military Outpost: Hotel Lobby", "Puesto militar: Vestíbulo del hotel"),
            ["RESOURCE.H38"] = new("RESOURCE.H38", 6, "Douglasville: Sushi's Hacker Loft & Workshop", "Douglasville: Loft y taller informático de Sushi"),
            ["RESOURCE.H40"] = new("RESOURCE.H40", 6, "Military Camp: Command Tent & Surveillance Terminal", "Campamento militar: Tienda de mando y terminal de vigilancia"),
            ["RESOURCE.H41"] = new("RESOURCE.H41", 6, "Military Camp: Security Keycard Gate", "Campamento militar: Puerta con tarjeta de seguridad"),
            ["RESOURCE.H42"] = new("RESOURCE.H42", 6, "Douglasville: Bank Vault Secret Escape Hatch", "Douglasville: Trampilla secreta de huida de la cámara del banco"),

            // Cutscenes & Finale
            ["RESOURCE.G01"] = new("RESOURCE.G01", 0, "Cutscene: Prologue & Hospital Arrival", "Cinemática: Prólogo y llegada al hospital"),
            ["RESOURCE.G02"] = new("RESOURCE.G02", 0, "Cutscene: Highway Journey & Chicago", "Cinemática: Viaje en carretera y Chicago"),
            ["RESOURCE.G03"] = new("RESOURCE.G03", 0, "Cutscene: Ambush & Desert Capture", "Cinemática: Emboscada y captura en el desierto"),
            ["RESOURCE.G04"] = new("RESOURCE.G04", 0, "Cutscene: The Fall & Hopi Legend", "Cinemática: La caída y leyenda hopi"),
            ["RESOURCE.G05"] = new("RESOURCE.G05", 0, "Cutscene: The Medium Séance & Truth", "Cinemática: Sesión de espiritismo y la verdad"),
            ["RESOURCE.I01"] = new("RESOURCE.I01", 6, "Finale: Showdown & Resolution", "Final: Desenlace y resolución"),
            ["RESOURCE.I03"] = new("RESOURCE.I03", 0, "Outro: End Credits & Epilogue", "Créditos finales y epílogo"),
        };

    private static readonly Dictionary<string, SceneInfo> ScenesByNameR2 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Intro & Prologue
            ["RESOURCE.001"] = new("RESOURCE.001", 1, "Intro: Airplane Interior", "Intro: Interior del avión"),
            ["RESOURCE.I01"] = new("RESOURCE.I01", 0, "Cutscene: Brian & Gina Flight", "Cinemática: Vuelo de Brian y Gina"),
            ["RESOURCE.I02"] = new("RESOURCE.I02", 0, "Cutscene: Cockpit Emergency", "Cinemática: Emergencia en cabina"),

            // Chapter 1: Mala Island / La isla de Mala
            ["RESOURCE.A05"] = new("RESOURCE.A05", 1, "Jungle: Crashed Plane & Lake", "Selva: Avión estrellado y lago"),
            ["RESOURCE.A08"] = new("RESOURCE.A08", 1, "Jungle: Lemur Path", "Selva: Sendero de los lémures"),
            ["RESOURCE.A09"] = new("RESOURCE.A09", 1, "Jungle: Quicksand", "Selva: Arenas movedizas"),
            ["RESOURCE.A10"] = new("RESOURCE.A10", 1, "Jungle: Monkey Tree", "Selva: Árbol del mono"),
            ["RESOURCE.A11"] = new("RESOURCE.A11", 1, "Jungle: Rope Bridge & Gorge", "Selva: Puente colgante y desfiladero"),

            // Chapter 2: Surfin' Mala / Surfeando en Mala
            ["RESOURCE.B01"] = new("RESOURCE.B01", 2, "Beach: Luana's Surf Shack & Bar", "Playa: Caseta de surf de Luana y bar"),
            ["RESOURCE.B03"] = new("RESOURCE.B03", 2, "Beach: Tiki Hut & Pier", "Playa: Cabaña tiki y muelle"),
            ["RESOURCE.B04"] = new("RESOURCE.B04", 2, "Military Camp: Guard Post & Exterior", "Campamento militar: Puesto de guardia y exterior"),
            ["RESOURCE.B04A"] = new("RESOURCE.B04A", 2, "Military Camp: Guard Post (Night)", "Campamento militar: Puesto de guardia (Noche)"),
            ["RESOURCE.B05"] = new("RESOURCE.B05", 2, "Military Camp: Colonel's Office", "Campamento militar: Despacho del coronel"),
            ["RESOURCE.B06"] = new("RESOURCE.B06", 2, "Military Camp: Detention Center", "Campamento militar: Centro de detención"),
            ["RESOURCE.B07"] = new("RESOURCE.B07", 2, "Military Camp: Communications Center", "Campamento militar: Centro de comunicaciones"),
            ["RESOURCE.B08"] = new("RESOURCE.B08", 2, "Military Camp: Vehicle Depot", "Campamento militar: Parque de vehículos"),
            ["RESOURCE.B09"] = new("RESOURCE.B09", 2, "Military Camp: Airstrip & Hangars", "Campamento militar: Pista y hangares"),
            ["RESOURCE.B20"] = new("RESOURCE.B20", 2, "Beach: Sunset Overlook", "Playa: Mirador al atardecer"),

            // Chapter 3: Simpler than an Amoeba / Más simple que una ameba
            ["RESOURCE.C01"] = new("RESOURCE.C01", 3, "Yacht: Deck & Ocean", "Yate: Cubierta y océano"),
            ["RESOURCE.C02"] = new("RESOURCE.C02", 3, "Yacht: Bridge & Controls", "Yate: Puente de mando"),
            ["RESOURCE.C03"] = new("RESOURCE.C03", 3, "Yacht: Engine Room", "Yate: Sala de máquinas"),
            ["RESOURCE.C04"] = new("RESOURCE.C04", 3, "Yacht: Rosa's Cabin", "Yate: Camarote de Rosa"),
            ["RESOURCE.C05"] = new("RESOURCE.C05", 3, "Underwater: Sunken Galleon", "Bajo el agua: Galeón hundido"),
            ["RESOURCE.C05B"] = new("RESOURCE.C05B", 3, "Underwater: Galleon Hold", "Bajo el agua: Bodega del galeón"),
            ["RESOURCE.C05C"] = new("RESOURCE.C05C", 3, "Underwater: Coral Reef", "Bajo el agua: Arrecife de coral"),
            ["RESOURCE.C09"] = new("RESOURCE.C09", 3, "Ocean: Sea Floor & Cave", "Océano: Fondo marino y cueva"),
            ["RESOURCE.C10"] = new("RESOURCE.C10", 3, "Yacht: Cargo Hold", "Yate: Bodega de carga"),
            ["RESOURCE.C11"] = new("RESOURCE.C11", 3, "Underwater: Sea Trench", "Bajo el agua: Fosa marina"),
            ["RESOURCE.C12"] = new("RESOURCE.C12", 3, "Yacht: Galley", "Yate: Cocina"),
            ["RESOURCE.C13"] = new("RESOURCE.C13", 3, "Underwater: Submarine Dock", "Bajo el agua: Muelle de submarinos"),
            ["RESOURCE.C14"] = new("RESOURCE.C14", 3, "Underwater: Deep Cavern", "Bajo el agua: Caverna profunda"),
            ["RESOURCE.C15"] = new("RESOURCE.C15", 3, "Underwater: Submarine Interior", "Bajo el agua: Interior del submarino"),
            ["RESOURCE.C16"] = new("RESOURCE.C16", 3, "Underwater: Submarine Airlock", "Bajo el agua: Esclusa del submarino"),
            ["RESOURCE.C17"] = new("RESOURCE.C17", 3, "Underwater: Sunken Ruins", "Bajo el agua: Ruinas sumergidas"),
            ["RESOURCE.C18"] = new("RESOURCE.C18", 3, "Underwater: Deep Trench Overlook", "Bajo el agua: Mirador de la fosa"),
            ["RESOURCE.C19"] = new("RESOURCE.C19", 3, "Underwater: Hidden Alcove", "Bajo el agua: Nicho oculto"),
            ["RESOURCE.C20"] = new("RESOURCE.C20", 3, "Underwater: Ancient Altar", "Bajo el agua: Altar ancestral"),
            ["RESOURCE.C21"] = new("RESOURCE.C21", 3, "Underwater: Escape Hatch", "Bajo el agua: Escotilla de escape"),

            // Chapter 4: The Guy Who Can Speak to Platypuses / El hombre que habla con los ornitorrincos
            ["RESOURCE.D06"] = new("RESOURCE.D06", 4, "Sanctuary: Platypus Lagoon", "Santuario: Laguna de los ornitorrincos"),
            ["RESOURCE.D08"] = new("RESOURCE.D08", 4, "Sanctuary: Joshua's Machine", "Santuario: La máquina de Joshua"),
            ["RESOURCE.D09"] = new("RESOURCE.D09", 4, "Sanctuary: Cliff Path", "Santuario: Sendero del acantilado"),
            ["RESOURCE.D10"] = new("RESOURCE.D10", 4, "Sanctuary: Archibald's Hut", "Santuario: Choza de Archibald"),
            ["RESOURCE.D11"] = new("RESOURCE.D11", 4, "Sanctuary: Lookout Point", "Santuario: Mirador"),
            ["RESOURCE.D12"] = new("RESOURCE.D12", 4, "Sanctuary: Rope Bridge", "Santuario: Puente de cuerda"),
            ["RESOURCE.D13"] = new("RESOURCE.D13", 4, "Sanctuary: Platypus Nest", "Santuario: Nido de ornitorrinco"),
            ["RESOURCE.D14"] = new("RESOURCE.D14", 4, "Sanctuary: Mountain Trail", "Santuario: Camino de montaña"),
            ["RESOURCE.D15"] = new("RESOURCE.D15", 4, "Sanctuary: Cave Entrance", "Santuario: Entrada de la cueva"),
            ["RESOURCE.D16"] = new("RESOURCE.D16", 4, "Sanctuary: Abandoned Campsite", "Santuario: Campamento abandonado"),
            ["RESOURCE.D17"] = new("RESOURCE.D17", 4, "Sanctuary: Waterfall & Stream", "Santuario: Cascada y arroyo"),

            // Chapter 5: Allerton Court / Allerton Court
            ["RESOURCE.E01"] = new("RESOURCE.E01", 5, "Allerton Court: Grand Foyer", "Allerton Court: Gran vestíbulo"),
            ["RESOURCE.E02"] = new("RESOURCE.E02", 5, "Allerton Court: Library", "Allerton Court: Biblioteca"),
            ["RESOURCE.E03"] = new("RESOURCE.E03", 5, "Allerton Court: Dining Room", "Allerton Court: Comedor"),
            ["RESOURCE.E04"] = new("RESOURCE.E04", 5, "Allerton Court: Study & Desk", "Allerton Court: Estudio y escritorio"),
            ["RESOURCE.E05"] = new("RESOURCE.E05", 5, "Allerton Court: Cellar & Crypt", "Allerton Court: Sótano y cripta"),
            ["RESOURCE.E07"] = new("RESOURCE.E07", 5, "Allerton Court: Conservatory", "Allerton Court: Invernadero"),
            ["RESOURCE.E08"] = new("RESOURCE.E08", 5, "Allerton Court: Upstairs Gallery", "Allerton Court: Galería superior"),
            ["RESOURCE.E10"] = new("RESOURCE.E10", 5, "Allerton Court: Hidden Vault", "Allerton Court: Cámara acorazada oculta"),
            ["RESOURCE.E11"] = new("RESOURCE.E11", 5, "Allerton Court: Secret Laboratory", "Allerton Court: Laboratorio secreto"),

            // Chapter 6: The Hidden Temple / El templo oculto
            ["RESOURCE.G01"] = new("RESOURCE.G01", 6, "Temple: Outer Courtyard", "Templo: Patio exterior"),
            ["RESOURCE.G02"] = new("RESOURCE.G02", 6, "Temple: Carved Antechamber", "Templo: Antecámara tallada"),
            ["RESOURCE.G03"] = new("RESOURCE.G03", 6, "Temple: Great Pyramid Hall", "Templo: Sala de la gran pirámide"),
            ["RESOURCE.G03B"] = new("RESOURCE.G03B", 6, "Temple: Pyramid Hall (Altar Lit)", "Templo: Sala de la pirámide (Altar encendido)"),
            ["RESOURCE.G04"] = new("RESOURCE.G04", 6, "Temple: Alien Portal", "Templo: Portal alienígena"),
            ["RESOURCE.G05"] = new("RESOURCE.G05", 6, "Temple: Celestial Observatory", "Templo: Observatorio celeste"),
            ["RESOURCE.G06"] = new("RESOURCE.G06", 6, "Temple: Crystal Chamber", "Templo: Cámara de cristal"),
            ["RESOURCE.G07"] = new("RESOURCE.G07", 6, "Temple: Power Conduits", "Templo: Conductos de energía"),
            ["RESOURCE.G08"] = new("RESOURCE.G08", 6, "Temple: Ancient Mechanism", "Templo: Mecanismo ancestral"),
            ["RESOURCE.G10"] = new("RESOURCE.G10", 6, "Spaceship: Command Bridge", "Nave espacial: Puente de mando"),
            ["RESOURCE.G11"] = new("RESOURCE.G11", 6, "Spaceship: Stasis Bay", "Nave espacial: Bahía de éxtasis"),
            ["RESOURCE.G12"] = new("RESOURCE.G12", 6, "Spaceship: Engine Core", "Nave espacial: Núcleo del motor"),
            ["RESOURCE.G13"] = new("RESOURCE.G13", 6, "Spaceship: Observation Deck", "Nave espacial: Cubierta de observación"),

            // Epilogue & Localized Overlays
            ["RESOURCE.H02"] = new("RESOURCE.H02", 6, "Epilogue: Island Shore", "Epílogo: Costa de la isla"),
            ["RESOURCE.H04"] = new("RESOURCE.H04", 6, "Epilogue: Finale Scene", "Epílogo: Escena final"),
            ["RESOURCE.H06"] = new("RESOURCE.H06", 6, "Epilogue: Credits Sequence", "Epílogo: Créditos"),
            ["RESOURCE.SP1"] = new("RESOURCE.SP1", 1, "Character Sprites: Chapter 1", "Personajes: Capítulo 1"),
            ["RESOURCE.SP2"] = new("RESOURCE.SP2", 2, "Character Sprites: Chapter 2", "Personajes: Capítulo 2"),
            ["RESOURCE.SP3"] = new("RESOURCE.SP3", 3, "Character Sprites: Chapter 3", "Personajes: Capítulo 3"),
            ["RESOURCE.SP4"] = new("RESOURCE.SP4", 4, "Character Sprites: Chapter 4", "Personajes: Capítulo 4"),
            ["RESOURCE.SP5"] = new("RESOURCE.SP5", 5, "Character Sprites: Chapter 5", "Personajes: Capítulo 5"),
        };

    private static readonly Dictionary<string, SceneInfo> ScenesByNameR3 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Chapter 1: Brian Basco Is Dead / Brian Basco ha muerto
            ["RESOURCE.A01"] = new("RESOURCE.A01", 1, "Cemetery: Brian's Grave", "Cementerio: Tumba de Brian"),
            ["RESOURCE.A02"] = new("RESOURCE.A02", 1, "Cemetery: Main Gate & Path", "Cementerio: Puerta principal y camino"),
            ["RESOURCE.A03"] = new("RESOURCE.A03", 1, "Cemetery: Crypt Exterior", "Cementerio: Exterior de la cripta"),
            ["RESOURCE.A04"] = new("RESOURCE.A04", 1, "Cemetery: Crypt Interior", "Cementerio: Interior de la cripta"),
            ["RESOURCE.A05"] = new("RESOURCE.A05", 1, "Cemetery: Mausoleum Hall", "Cementerio: Sala del mausoleo"),
            ["RESOURCE.A06"] = new("RESOURCE.A06", 1, "Cemetery: Chapel", "Cementerio: Capilla"),
            ["RESOURCE.A07"] = new("RESOURCE.A07", 1, "Cemetery: Caretaker's Hut", "Cementerio: Caseta del sepulturero"),
            ["RESOURCE.A08"] = new("RESOURCE.A08", 1, "Cemetery: Stone Wall & Trees", "Cementerio: Muro de piedra y árboles"),
            ["RESOURCE.A09"] = new("RESOURCE.A09", 1, "Cemetery: Graveyard Grounds", "Cementerio: Terreno del camposanto"),
            ["RESOURCE.A10"] = new("RESOURCE.A10", 1, "Cemetery: Memorial Walk", "Cementerio: Paseo memorial"),
            ["RESOURCE.A11"] = new("RESOURCE.A11", 1, "Cemetery: Escape Route", "Cementerio: Ruta de escape"),
            ["RESOURCE.A12"] = new("RESOURCE.A12", 1, "Cemetery: Secret Passage", "Cementerio: Pasadizo secreto"),

            // Chapter 2: The Art of Running Away / El arte de escapar
            ["RESOURCE.B00"] = new("RESOURCE.B00", 2, "Happy Dale: Exterior Overview", "Happy Dale: Vista exterior"),
            ["RESOURCE.B01"] = new("RESOURCE.B01", 2, "Happy Dale: Brian's Room", "Happy Dale: Habitación de Brian"),
            ["RESOURCE.B02"] = new("RESOURCE.B02", 2, "Happy Dale: Main Corridor", "Happy Dale: Pasillo principal"),
            ["RESOURCE.B03"] = new("RESOURCE.B03", 2, "Happy Dale: Common Recreation Room", "Happy Dale: Sala de recreo"),
            ["RESOURCE.B04"] = new("RESOURCE.B04", 2, "Happy Dale: Nurse Station", "Happy Dale: Control de enfermería"),
            ["RESOURCE.B05"] = new("RESOURCE.B05", 2, "Happy Dale: Dr. Bennett's Office", "Happy Dale: Despacho del Dr. Bennett"),
            ["RESOURCE.B07"] = new("RESOURCE.B07", 2, "Happy Dale: Security Checkpoint", "Happy Dale: Control de seguridad"),
            ["RESOURCE.B08"] = new("RESOURCE.B08", 2, "Happy Dale: Courtyard & Garden", "Happy Dale: Patio y jardín"),
            ["RESOURCE.B10"] = new("RESOURCE.B10", 2, "Happy Dale: Therapy Room", "Happy Dale: Sala de terapia"),
            ["RESOURCE.B11"] = new("RESOURCE.B11", 2, "Happy Dale: Ernie's Room", "Happy Dale: Habitación de Ernie"),
            ["RESOURCE.B12"] = new("RESOURCE.B12", 2, "Happy Dale: Gabbo's Room", "Happy Dale: Habitación de Gabbo"),
            ["RESOURCE.B13"] = new("RESOURCE.B13", 2, "Happy Dale: Marcelo's Quarters", "Happy Dale: Cuarto de Marcelo"),
            ["RESOURCE.B14"] = new("RESOURCE.B14", 2, "Happy Dale: Restrooms", "Happy Dale: Baños"),
            ["RESOURCE.B15"] = new("RESOURCE.B15", 2, "Happy Dale: Storage & Utility", "Happy Dale: Almacén y mantenimiento"),
            ["RESOURCE.B16"] = new("RESOURCE.B16", 2, "Happy Dale: Laundry Room", "Happy Dale: Lavandería"),
            ["RESOURCE.B19"] = new("RESOURCE.B19", 2, "Happy Dale: Ventilation Shaft", "Happy Dale: Conducto de ventilación"),
            ["RESOURCE.B20"] = new("RESOURCE.B20", 2, "Happy Dale: Perimeter Wall & Fence", "Happy Dale: Valla perimetral"),

            // Chapter 3: A Timely Suicide / Un suicidio bien oportuno
            ["RESOURCE.C01"] = new("RESOURCE.C01", 3, "Lake Cabin: Main Living Area", "Cabaña del lago: Salón principal"),
            ["RESOURCE.C02"] = new("RESOURCE.C02", 3, "Lake Cabin: Kitchen & Counter", "Cabaña del lago: Cocina y encimera"),
            ["RESOURCE.C03"] = new("RESOURCE.C03", 3, "Lake Cabin: Bedroom", "Cabaña del lago: Dormitorio"),
            ["RESOURCE.C06"] = new("RESOURCE.C06", 3, "Lake Cabin: Porch & Deck", "Cabaña del lago: Porche y terraza"),
            ["RESOURCE.C07"] = new("RESOURCE.C07", 3, "Lake Shore: Wooden Pier", "Orilla del lago: Muelle de madera"),
            ["RESOURCE.C08"] = new("RESOURCE.C08", 3, "Lake Shore: Boathouse & Shed", "Orilla del lago: Cobertizo de botes"),
            ["RESOURCE.C09"] = new("RESOURCE.C09", 3, "Lake Forest: Wooded Path", "Bosque del lago: Sendero arbolado"),
            ["RESOURCE.C10"] = new("RESOURCE.C10", 3, "Lake Stream: Old Wooden Bridge", "Arroyo del lago: Puente viejo"),
            ["RESOURCE.C11"] = new("RESOURCE.C11", 3, "Lake Forest: Forest Clearing", "Bosque del lago: Claro del bosque"),
            ["RESOURCE.C12"] = new("RESOURCE.C12", 3, "Lake Cabin: Storage Annex", "Cabaña del lago: Anexo de almacenaje"),
            ["RESOURCE.C15"] = new("RESOURCE.C15", 3, "Lake Cabin: Investigation Room", "Cabaña del lago: Sala de investigación"),

            // Chapter 4: An Unexpected Ally / Un aliado inesperado
            ["RESOURCE.D00"] = new("RESOURCE.D00", 4, "Morgue: Basement Hallway", "Depósito de cadáveres: Pasillo del sótano"),
            ["RESOURCE.D01"] = new("RESOURCE.D01", 4, "Morgue: Autopsy Lab", "Depósito de cadáveres: Sala de autopsias"),
            ["RESOURCE.D02"] = new("RESOURCE.D02", 4, "Morgue: Cold Storage & Lockers", "Depósito de cadáveres: Cámaras frigoríficas"),
            ["RESOURCE.D03"] = new("RESOURCE.D03", 4, "Morgue: Incinerator Room", "Depósito de cadáveres: Sala del incinerador"),
            ["RESOURCE.D04"] = new("RESOURCE.D04", 4, "Morgue: Preparation Chamber", "Depósito de cadáveres: Sala de preparación"),
            ["RESOURCE.D05"] = new("RESOURCE.D05", 4, "Morgue: Medical Records Office", "Depósito de cadáveres: Archivo médico"),
            ["RESOURCE.D06"] = new("RESOURCE.D06", 4, "Morgue: Supply Closet", "Depósito de cadáveres: Armario de suministros"),
            ["RESOURCE.D07"] = new("RESOURCE.D07", 4, "Morgue: Loading Bay", "Depósito de cadáveres: Bahía de carga"),
            ["RESOURCE.D10"] = new("RESOURCE.D10", 4, "Morgue: Hatch & Air Duct", "Depósito de cadáveres: Escotilla y conducto"),
            ["RESOURCE.D11"] = new("RESOURCE.D11", 4, "Morgue: Emergency Exit", "Depósito de cadáveres: Salida de emergencia"),

            // Chapter 5: Deconstructing Brian / Brian en todos sus estados
            ["RESOURCE.E01"] = new("RESOURCE.E01", 5, "Flashback: Tanton Manor & Camp", "Recuerdo: Mansión Tanton y campamento"),

            // Chapter 6: The End Is Here / El final está cerca
            ["RESOURCE.F01"] = new("RESOURCE.F01", 6, "New York: Dark Alleyways", "Nueva York: Callejones oscuros"),
            ["RESOURCE.F03"] = new("RESOURCE.F03", 6, "New York: Construction Crane & Rooftops", "Nueva York: Grúa de construcción y azoteas"),
            ["RESOURCE.F04"] = new("RESOURCE.F04", 6, "New York: Final Showdown", "Nueva York: Desenlace final"),

            // Epilogue & Cutscenes & Localized Overlays
            ["RESOURCE.H01"] = new("RESOURCE.H01", 7, "Epilogue: Flashback & Revelation", "Epílogo: Recuerdos y revelación"),
            ["RESOURCE.002"] = new("RESOURCE.002", 0, "Cinematic Cutscene Animations", "Animaciones de cinemáticas"),
            ["RESOURCE.SP1"] = new("RESOURCE.SP1", 1, "Spanish Overlay: Chapter 1", "Textos español: Capítulo 1"),
            ["RESOURCE.SP2"] = new("RESOURCE.SP2", 2, "Spanish Overlay: Chapter 2", "Textos español: Capítulo 2"),
            ["RESOURCE.SP3"] = new("RESOURCE.SP3", 3, "Spanish Overlay: Chapter 3", "Textos español: Capítulo 3"),
            ["RESOURCE.SP5"] = new("RESOURCE.SP5", 5, "Spanish Overlay: Chapter 5", "Textos español: Capítulo 5"),
            ["RESOURCE.SP6"] = new("RESOURCE.SP6", 6, "Spanish Overlay: Chapter 6", "Textos español: Capítulo 6"),
            ["RESOURCE.SP7"] = new("RESOURCE.SP7", 7, "Spanish Overlay: Epilogue", "Textos español: Epílogo"),
        };

    private static readonly Dictionary<string, (string En, string Es)> AudioNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RESOURCE.M01"] = ("Main Theme & Title", "Tema principal y título"),
            ["RESOURCE.M02"] = ("Hospital & Investigation", "Hospital e investigación"),
            ["RESOURCE.M03"] = ("Museum of Natural History", "Museo de Historia Natural"),
            ["RESOURCE.M04"] = ("Desert & Ghost Town", "Desierto y pueblo fantasma"),
            ["RESOURCE.M05"] = ("Hopi Village & Sacred Crypts", "Poblado hopi y criptas sagradas"),
            ["RESOURCE.M06"] = ("Action & Finale", "Acción y final"),
            ["RESOURCE.002"] = ("Cinematic Audio", "Audio de cinemáticas"),
            ["RESOURCE.S01"] = ("Hospital Ambience & SFX", "Hospital: Ambiente y efectos"),
            ["RESOURCE.S02"] = ("Museum Ambience & SFX", "Museo: Ambiente y efectos"),
            ["RESOURCE.S03"] = ("Desert Ambience & SFX", "Desierto: Ambiente y efectos"),
            ["RESOURCE.S04"] = ("Douglasville Ambience & SFX", "Douglasville: Ambiente y efectos"),
            ["RESOURCE.S05"] = ("Pueblo Ambience & SFX", "Pueblo: Ambiente y efectos"),
            ["RESOURCE.S06"] = ("Crypts & Caves Ambience & SFX", "Criptas y cuevas: Ambiente y efectos"),
            ["RESOURCE.S07"] = ("Hopi Village Ambience & SFX", "Poblado hopi: Ambiente y efectos"),
            ["RESOURCE.S08"] = ("Military Camp Ambience & SFX", "Campamento militar: Ambiente y efectos"),
            ["RESOURCE.S09"] = ("Action Ambience & SFX", "Acción: Ambiente y efectos"),
            ["RESOURCE.S10"] = ("Misc Sound Effects", "Efectos de sonido varios"),
        };

    private static readonly Dictionary<string, (string En, string Es)> AudioNamesR2 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RESOURCE.M01"] = ("Mala Island Theme", "Tema de la isla de Mala"),
            ["RESOURCE.M02"] = ("Surfin' Mala Theme", "Tema de Surfeando en Mala"),
            ["RESOURCE.M03"] = ("Under the Sea & Galleon Theme", "Bajo el mar y el galeón"),
            ["RESOURCE.M04"] = ("Platypus Bay Theme", "Bahía de los ornitorrincos"),
            ["RESOURCE.M05"] = ("Allerton Court Manor Theme", "Mansión Allerton Court"),
            ["RESOURCE.M06"] = ("The Hidden Temple & Finale Theme", "El templo oculto y tema final"),
            ["RESOURCE.S00"] = ("Jungle & Nature Ambience", "Ambiente de selva y naturaleza"),
            ["RESOURCE.S01"] = ("Camp & Military Ambience", "Ambiente militar y campamento"),
            ["RESOURCE.S02"] = ("Underwater & Ocean Ambience", "Ambiente oceánico y submarino"),
            ["RESOURCE.S03"] = ("Sanctuary & Island Ambience", "Ambiente del santuario y la isla"),
            ["RESOURCE.S04"] = ("Manor & Indoors Ambience", "Ambiente de la mansión e interiores"),
            ["RESOURCE.S05"] = ("Ancient Temple Ambience", "Ambiente del templo ancestral"),
            ["RESOURCE.S06"] = ("Sound Effects", "Efectos de sonido"),
        };

    private static readonly Dictionary<string, (string En, string Es)> AudioNamesR3 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RESOURCE.S00"] = ("Menu & Interface Sounds", "Sonidos de menú e interfaz"),
            ["RESOURCE.S01"] = ("Chapter 1 Sound Effects", "Efectos de sonido: Capítulo 1"),
            ["RESOURCE.S02"] = ("Chapter 2 Sound Effects", "Efectos de sonido: Capítulo 2"),
            ["RESOURCE.S03"] = ("Chapter 3 Sound Effects", "Efectos de sonido: Capítulo 3"),
            ["RESOURCE.S05"] = ("Chapter 5 Sound Effects", "Efectos de sonido: Capítulo 5"),
            ["RESOURCE.S06"] = ("Chapter 6 Sound Effects", "Efectos de sonido: Capítulo 6"),
            ["RESOURCE.S07"] = ("Epilogue Sound Effects", "Efectos de sonido: Epílogo"),
        };

    private static readonly Dictionary<string, (string En, string Es)> CategoryNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [VirtualFileSystem.ScenesFolder] = ("Scenes", "Escenas"),
            [VirtualFileSystem.MusicFolder] = ("Music", "Música"),
            [VirtualFileSystem.AmbientFolder] = ("Ambient & SFX", "Sonido ambiente y efectos"),
            [VirtualFileSystem.CinematicFolder] = ("Cinematic Audio", "Audio de cinemáticas"),
            [VirtualFileSystem.VoiceFolder] = ("Voice", "Voces"),
            [VirtualFileSystem.LipSyncFolder] = ("Lip-sync", "Sincronización labial"),
            [VirtualFileSystem.VideoFolder] = ("Video", "Vídeos"),
            [VirtualFileSystem.DialogueFolder] = ("Dialogue", "Diálogos"),
            [VirtualFileSystem.CharactersFolder] = ("Characters", "Personajes"),
            [VirtualFileSystem.GlobalFolder] = ("Global Data", "Datos globales"),
            [VirtualFileSystem.SavegamesFolder] = ("Saved Games", "Partidas guardadas"),
        };

    private static readonly IReadOnlyDictionary<int, ChapterInfo>[] ChaptersPerGame =
        [ChaptersByNumber, ChaptersByNumberR2, ChaptersByNumberR3];

    private static readonly IReadOnlyDictionary<string, SceneInfo>[] ScenesPerGame =
        [ScenesByName, ScenesByNameR2, ScenesByNameR3];

    private static readonly IReadOnlyDictionary<string, (string En, string Es)>[] AudioPerGame =
        [AudioNames, AudioNamesR2, AudioNamesR3];

    public static bool IsSpanish(string? language) =>
        string.Equals(language, "es", StringComparison.OrdinalIgnoreCase) ||
        (language is null && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the official chapter information if known.</summary>
    public static ChapterInfo? GetChapter(int chapterNumber, GameVersion game = GameVersion.Runaway1) =>
        ChaptersPerGame[Math.Clamp((int)game, 0, 2)].GetValueOrDefault(chapterNumber);

    /// <summary>Returns friendly scene title with readable name first and internal name after (e.g. "Ch. 1 (\"Wake Up, Brian!\"): Hospital: Gina's Room (RESOURCE.A01)").</summary>
    public static string GetSceneTitle(string archiveName, string? language = null, GameVersion game = GameVersion.Runaway1)
    {
        string baseName = Path.GetFileName(archiveName);
        bool es = IsSpanish(language);
        int gameIdx = Math.Clamp((int)game, 0, 2);
        var scenes = ScenesPerGame[gameIdx];
        var chapters = ChaptersPerGame[gameIdx];

        if (scenes.TryGetValue(baseName, out SceneInfo? info))
        {
            string loc = es ? info.NameEs : info.NameEn;
            if (info.Chapter > 0 && chapters.TryGetValue(info.Chapter, out ChapterInfo? ch))
            {
                string chTitle = es ? ch.TitleEs : ch.TitleEn;
                return $"Ch. {info.Chapter} (\"{chTitle}\"): {loc} ({baseName})";
            }
            return $"{loc} ({baseName})";
        }

        return baseName;
    }

    /// <summary>Returns friendly title for an audio archive with readable name first (e.g. "Main Theme & Title (RESOURCE.M01)").</summary>
    public static string GetAudioTitle(string archiveName, string? language = null, GameVersion game = GameVersion.Runaway1)
    {
        string baseName = Path.GetFileName(archiveName);
        bool es = IsSpanish(language);
        var audio = AudioPerGame[Math.Clamp((int)game, 0, 2)];

        if (audio.TryGetValue(baseName, out var pair))
        {
            string title = es ? pair.Es : pair.En;
            return $"{title} ({baseName})";
        }

        return baseName;
    }

    /// <summary>Returns friendly name for a top-level virtual filesystem folder.</summary>
    public static string GetCategoryTitle(string categoryName, string? language = null)
    {
        bool es = IsSpanish(language);
        if (CategoryNames.TryGetValue(categoryName, out var pair))
            return es ? pair.Es : pair.En;
        return categoryName;
    }

    private static readonly Dictionary<string, (string En, string Es)> GlobalArchiveTitles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["RESOURCE.000"] = ("Global Assets & Fonts", "Recursos globales y fuentes"),
            ["RESOURCE.IFZ"] = ("User Interface & HUD", "Interfaz de usuario y menús"),
            ["RESOURCE.PSE"] = ("Pause Screen Graphics", "Pantalla de pausa"),
            ["RESOURCE.TAB"] = ("Scene Geometry & Waypoints", "Geometría de escenas y rutas"),
            ["RESOURCE.FNT"] = ("Outlined Font Library", "Fuentes con contorno"),
            ["RESOURCE.005"] = ("Chapter Interaction Database", "Base de datos de interacciones"),
            ["RESOURCE.001"] = ("Character Sprite Library", "Biblioteca de sprites de personajes"),
        };

    private static readonly Dictionary<int, (string En, string Es)> HollywoodResidentSounds = new()
    {
        [0x55] = ("Footstep (Wood A)", "Pisada (Madera A)"),
        [0x56] = ("UI Click / Interaction A", "Clic de interfaz / Interacción A"),
        [0x57] = ("Footstep (Stone A)", "Pisada (Piedra A)"),
        [0x58] = ("Door Latch / Click", "Cerrojo / Clic"),
        [0x59] = ("Footstep (Wood B)", "Pisada (Madera B)"),
        [0x5a] = ("Footstep (Stone B)", "Pisada (Piedra B)"),
        [0x5b] = ("Object Pickup / Use", "Recoger / Usar objeto"),
        [0x5c] = ("Page Turn / Paper", "Pasar página / Papel"),
        [0x5d] = ("Footstep (Wood C)", "Pisada (Madera C)"),
        [0x5e] = ("Footstep (Stone C)", "Pisada (Piedra C)"),
        [0x5f] = ("UI Cancel / Back", "Cancelar / Volver"),
        [0x60] = ("Inventory Open", "Abrir inventario"),
        [0x61] = ("Footstep (Stone D)", "Pisada (Piedra D)"),
        [0x62] = ("Inventory Close", "Cerrar inventario"),
    };

    /// <summary>Returns friendly name for a global archive (e.g. "User Interface & HUD (RESOURCE.IFZ)").</summary>
    public static string GetGlobalTitle(string archiveName, string? language = null, GameVersion game = GameVersion.Runaway1)
    {
        string baseName = Path.GetFileName(archiveName);
        bool es = IsSpanish(language);

        if (GlobalArchiveTitles.TryGetValue(baseName, out var pair))
        {
            string title = es ? pair.Es : pair.En;
            return $"{title} ({baseName})";
        }

        return baseName;
    }

    /// <summary>Returns friendly name for a Hollywood Monsters resident sound effect slot (0x55..0x62).</summary>
    public static string GetHollywoodResidentSoundName(int slot, string? language = null)
    {
        bool es = IsSpanish(language);
        if (HollywoodResidentSounds.TryGetValue(slot, out var pair))
            return es ? pair.Es : pair.En;
        return es ? "Efecto residente" : "Resident SFX";
    }

    private static readonly Dictionary<(GameVersion Game, int Index), (string En, string Es)> KnownGlobalAssetNames = new()
    {
        // Runaway 1 (RESOURCE.000)
        [(GameVersion.Runaway1, 118)] = ("evaluation version badge (48×48)", "insignia versión de evaluación (48×48)"),
        [(GameVersion.Runaway1, 119)] = ("inventory items strip — Chapter 1 (80×1840)", "tira de objetos de inventario — Capítulo 1 (80×1840)"),
        [(GameVersion.Runaway1, 120)] = ("inventory items strip — Chapter 2 (80×2079)", "tira de objetos de inventario — Capítulo 2 (80×2079)"),
        [(GameVersion.Runaway1, 121)] = ("inventory items strip — Chapter 3 (80×3360)", "tira de objetos de inventario — Capítulo 3 (80×3360)"),
        [(GameVersion.Runaway1, 122)] = ("inventory items strip — Chapter 4 (80×3760)", "tira de objetos de inventario — Capítulo 4 (80×3760)"),
        [(GameVersion.Runaway1, 123)] = ("inventory items strip — Chapter 5 (80×1360)", "tira de objetos de inventario — Capítulo 5 (80×1360)"),
        [(GameVersion.Runaway1, 124)] = ("inventory items strip — Chapter 6 (80×1920)", "tira de objetos de inventario — Capítulo 6 (80×1920)"),
        [(GameVersion.Runaway1, 125)] = ("font bitmap (regular)", "mapa de bits de fuente (normal)"),
        [(GameVersion.Runaway1, 126)] = ("glyph table (regular)", "tabla de glifos (normal)"),
        [(GameVersion.Runaway1, 127)] = ("UI strip — menu options (700×74)", "tira de IU — opciones de menú (700×74)"),
        [(GameVersion.Runaway1, 128)] = ("UI strip — save game (700×74)", "tira de IU — guardar partida (700×74)"),
        [(GameVersion.Runaway1, 129)] = ("UI strip — load game (700×74)", "tira de IU — cargar partida (700×74)"),
        [(GameVersion.Runaway1, 130)] = ("UI strip — controls (700×74)", "tira de IU — controles (700×74)"),
        [(GameVersion.Runaway1, 131)] = ("inventory screen backdrop (1024×600)", "fondo de pantalla de inventario (1024×600)"),
        [(GameVersion.Runaway1, 132)] = ("main menu panel (1024×600)", "panel del menú principal (1024×600)"),
        [(GameVersion.Runaway1, 133)] = ("inventory grid backing (768×576)", "rejilla de fondo del inventario (768×576)"),
        [(GameVersion.Runaway1, 135)] = ("talking head — Brian inventory animation 1", "busto parlante — animación Brian inventario 1"),
        [(GameVersion.Runaway1, 136)] = ("talking head — Brian inventory animation 2", "busto parlante — animación Brian inventario 2"),
        [(GameVersion.Runaway1, 137)] = ("talking head — Brian inventory animation 3", "busto parlante — animación Brian inventario 3"),
        [(GameVersion.Runaway1, 138)] = ("talking head — Brian inventory animation 4", "busto parlante — animación Brian inventario 4"),
        [(GameVersion.Runaway1, 139)] = ("talking head — Brian inventory animation 5", "busto parlante — animación Brian inventario 5"),
        [(GameVersion.Runaway1, 140)] = ("talking head — Brian inventory animation 6", "busto parlante — animación Brian inventario 6"),
        [(GameVersion.Runaway1, 141)] = ("talking head — Brian inventory animation 7", "busto parlante — animación Brian inventario 7"),
        [(GameVersion.Runaway1, 142)] = ("talking head — Brian inventory animation 8", "busto parlante — animación Brian inventario 8"),
        [(GameVersion.Runaway1, 143)] = ("item close-up — Mayan crucifix in casing (1024×600)", "primer plano — crucifijo maya en funda (1024×600)"),
        [(GameVersion.Runaway1, 144)] = ("item close-up — cleaned Mayan crucifix (1024×600)", "primer plano — crucifijo maya limpio (1024×600)"),
        [(GameVersion.Runaway1, 145)] = ("item close-up — Mayan crucifix key (1024×600)", "primer plano — llave del crucifijo maya (1024×600)"),
        [(GameVersion.Runaway1, 146)] = ("item close-up — Douglas & Co. Mining map (1024×600)", "primer plano — mapa minero Douglas & Co. (1024×600)"),
        [(GameVersion.Runaway1, 147)] = ("item close-up — severed finger in jar (1024×600)", "primer plano — dedo amputado en frasco (1024×600)"),
        [(GameVersion.Runaway1, 148)] = ("font bitmap (bold)", "mapa de bits de fuente (negrita)"),
        [(GameVersion.Runaway1, 149)] = ("glyph table (bold)", "tabla de glifos (negrita)"),
        [(GameVersion.Runaway1, 150)] = ("UI interaction click / select SFX", "efecto de sonido clic / selección de IU"),
        [(GameVersion.Runaway1, 151)] = ("item close-up — Hopi sacred bush branch (1024×600)", "primer plano — rama del arbusto sagrado hopi (1024×600)"),
        [(GameVersion.Runaway1, 152)] = ("end credits roll strip (400×5870)", "tira de créditos finales (400×5870)"),
        [(GameVersion.Runaway1, 154)] = ("end credits roll backing (400×5870)", "fondo de créditos finales (400×5870)"),
        [(GameVersion.Runaway1, 155)] = ("options menu volume knob animation", "animación botón de volumen del menú de opciones"),
        [(GameVersion.Runaway1, 156)] = ("options menu brightness slider animation", "animación deslizador de brillo del menú de opciones"),
        [(GameVersion.Runaway1, 157)] = ("CD disc loading icon (48×48)", "icono de carga disco CD (48×48)"),
        [(GameVersion.Runaway1, 158)] = ("item close-up — Mojave bank brochure (1024×600)", "primer plano — folleto del banco de Mojave (1024×600)"),
        [(GameVersion.Runaway1, 159)] = ("chapter title labels animation (1-6)", "animación títulos de capítulo (1-6)"),
        [(GameVersion.Runaway1, 161)] = ("font bitmap (small)", "mapa de bits de fuente (pequeña)"),
        [(GameVersion.Runaway1, 162)] = ("glyph table (small)", "tabla de glifos (pequeña)"),

        // Runaway 2 (RESOURCE.000)
        [(GameVersion.Runaway2, 80)] = ("chapter selection board — start / prologue (1024×600)", "panel de selección de capítulo — inicio / prólogo (1024×600)"),
        [(GameVersion.Runaway2, 81)] = ("chapter selection board — Chapter 1 completed (1024×600)", "panel de selección de capítulo — Capítulo 1 completado (1024×600)"),
        [(GameVersion.Runaway2, 82)] = ("chapter selection board — Chapter 2 completed (1024×600)", "panel de selección de capítulo — Capítulo 2 completado (1024×600)"),
        [(GameVersion.Runaway2, 83)] = ("chapter selection board — Chapter 3 completed (1024×600)", "panel de selección de capítulo — Capítulo 3 completado (1024×600)"),
        [(GameVersion.Runaway2, 84)] = ("chapter selection board — Chapter 4 completed (1024×600)", "panel de selección de capítulo — Capítulo 4 completado (1024×600)"),
        [(GameVersion.Runaway2, 85)] = ("chapter selection board — Chapter 5 completed (1024×600)", "panel de selección de capítulo — Capítulo 5 completado (1024×600)"),
        [(GameVersion.Runaway2, 86)] = ("chapter selection board — Chapter 6 completed (1024×600)", "panel de selección de capítulo — Capítulo 6 completado (1024×600)"),
        [(GameVersion.Runaway2, 112)] = ("cursor atlas — standard pointers (904×540)", "atlas de cursores — punteros estándar (904×540)"),
        [(GameVersion.Runaway2, 113)] = ("cursor atlas — action icons (904×540)", "atlas de cursores — iconos de acción (904×540)"),
        [(GameVersion.Runaway2, 114)] = ("cursor atlas — magnifying glass (904×540)", "atlas de cursores — lupa (904×540)"),
        [(GameVersion.Runaway2, 115)] = ("cursor atlas — mouth / talk (904×540)", "atlas de cursores — boca / hablar (904×540)"),
        [(GameVersion.Runaway2, 116)] = ("cursor atlas — gears / interact (904×540)", "atlas de cursores — engranajes / interactuar (904×540)"),
        [(GameVersion.Runaway2, 117)] = ("cursor atlas — crosshair / target (904×540)", "atlas de cursores — mira / apuntar (904×540)"),
        [(GameVersion.Runaway2, 120)] = ("inventory panel backing (672×336)", "panel de inventario (672×336)"),
        [(GameVersion.Runaway2, 121)] = ("inventory items frame (672×336)", "marco de objetos de inventario (672×336)"),
        [(GameVersion.Runaway2, 123)] = ("font bitmap (regular)", "mapa de bits de fuente (normal)"),
        [(GameVersion.Runaway2, 124)] = ("glyph table (regular)", "tabla de glifos (normal)"),
        [(GameVersion.Runaway2, 125)] = ("font bitmap (dialogue)", "mapa de bits de fuente (diálogos)"),
        [(GameVersion.Runaway2, 126)] = ("glyph table (dialogue)", "tabla de glifos (diálogos)"),
        [(GameVersion.Runaway2, 127)] = ("UI strip — menu options (700×74)", "tira de IU — opciones de menú (700×74)"),
        [(GameVersion.Runaway2, 128)] = ("UI strip — save game (700×74)", "tira de IU — guardar partida (700×74)"),
        [(GameVersion.Runaway2, 129)] = ("UI strip — load game (700×74)", "tira de IU — cargar partida (700×74)"),
        [(GameVersion.Runaway2, 130)] = ("UI strip — controls (700×74)", "tira de IU — controles (700×74)"),
        [(GameVersion.Runaway2, 133)] = ("inventory backing (768×576)", "fondo del inventario (768×576)"),
        [(GameVersion.Runaway2, 140)] = ("chapter title card — Chapter 1: Trapped in the Jungle (1024×600)", "pantalla de capítulo — Capítulo 1: La isla de Mala (1024×600)"),
        [(GameVersion.Runaway2, 141)] = ("chapter title card — Chapter 2: Surfin' Mala (1024×600)", "pantalla de capítulo — Capítulo 2: Surfeando en Mala (1024×600)"),
        [(GameVersion.Runaway2, 142)] = ("chapter title card — Chapter 3: Simpler than an Amoeba (1024×600)", "pantalla de capítulo — Capítulo 3: Más simple que una ameba (1024×600)"),
        [(GameVersion.Runaway2, 143)] = ("chapter title card — Chapter 4: He Who Knows Does Not Speak (1024×600)", "pantalla de capítulo — Capítulo 4: El hombre que habla con los ornitorrincos (1024×600)"),
        [(GameVersion.Runaway2, 144)] = ("chapter title card — Chapter 5: Shipped to the Past (1024×600)", "pantalla de capítulo — Capítulo 5: Allerton Court (1024×600)"),
        [(GameVersion.Runaway2, 145)] = ("chapter title card — Chapter 6: The Hidden Beacon of Avernus (1024×600)", "pantalla de capítulo — Capítulo 6: El templo oculto (1024×600)"),
        [(GameVersion.Runaway2, 146)] = ("font bitmap (bold)", "mapa de bits de fuente (negrita)"),
        [(GameVersion.Runaway2, 147)] = ("glyph table (bold)", "tabla de glifos (negrita)"),
        [(GameVersion.Runaway2, 149)] = ("end credits roll strip (400×8510)", "tira de créditos finales (400×8510)"),
        [(GameVersion.Runaway2, 150)] = ("end credits character animation 1", "animación de créditos finales 1"),
        [(GameVersion.Runaway2, 151)] = ("end credits roll backing (400×8510)", "fondo de créditos finales (400×8510)"),
        [(GameVersion.Runaway2, 152)] = ("end credits character animation 2", "animación de créditos finales 2"),
        [(GameVersion.Runaway2, 154)] = ("luxury yacht deck close-up (1024×600)", "cubierta del yate (1024×600)"),
        [(GameVersion.Runaway2, 269)] = ("options menu panel (1024×600)", "panel de menú de opciones (1024×600)"),

        // Runaway 3 (RESOURCE.000)
        [(GameVersion.Runaway3, 10)] = ("main menu level selection (1280×720)", "menú principal: selección de nivel (1280×720)"),
        [(GameVersion.Runaway3, 13)] = ("bitmap 200×60 — UI button / logo", "bitmap 200×60 — botón de IU / logotipo"),
        [(GameVersion.Runaway3, 14)] = ("bitmap 600×100 — title logo: Runaway A Twist of Fate", "bitmap 600×100 — logotipo: Runaway A Twist of Fate"),
        [(GameVersion.Runaway3, 40)] = ("scene selection board — Prologue Brian (1280×720)", "panel de selección — Prólogo Brian (1280×720)"),
        [(GameVersion.Runaway3, 41)] = ("scene selection board — Prologue Gina (1280×720)", "panel de selección — Prólogo Gina (1280×720)"),
        [(GameVersion.Runaway3, 42)] = ("scene selection board — Chapter 1 (1280×720)", "panel de selección — Capítulo 1 (1280×720)"),
        [(GameVersion.Runaway3, 43)] = ("scene selection board — Chapter 2 (1280×720)", "panel de selección — Capítulo 2 (1280×720)"),
        [(GameVersion.Runaway3, 44)] = ("scene selection board — Chapter 3 (1280×720)", "panel de selección — Capítulo 3 (1280×720)"),
        [(GameVersion.Runaway3, 45)] = ("scene selection board — Chapter 4 (1280×720)", "panel de selección — Capítulo 4 (1280×720)"),
        [(GameVersion.Runaway3, 46)] = ("scene selection board — Chapter 5 Brian (1280×720)", "panel de selección — Capítulo 5 Brian (1280×720)"),
        [(GameVersion.Runaway3, 47)] = ("scene selection board — Chapter 5 Gina (1280×720)", "panel de selección — Capítulo 5 Gina (1280×720)"),
        [(GameVersion.Runaway3, 48)] = ("scene selection board — Chapter 5 Kordsmeier (1280×720)", "panel de selección — Capítulo 5 Kordsmeier (1280×720)"),
        [(GameVersion.Runaway3, 49)] = ("scene selection board — Chapter 6 Brian (1280×720)", "panel de selección — Capítulo 6 Brian (1280×720)"),
        [(GameVersion.Runaway3, 50)] = ("scene selection board — Chapter 6 Gina (1280×720)", "panel de selección — Capítulo 6 Gina (1280×720)"),
        [(GameVersion.Runaway3, 103)] = ("bitmap 1280×720 — pause menu screen", "bitmap 1280×720 — pantalla del menú de pausa"),
        [(GameVersion.Runaway3, 104)] = ("bitmap 128×192 — loading spinner atlas", "bitmap 128×192 — atlas de rueda de carga"),
        [(GameVersion.Runaway3, 112)] = ("bitmap 1280×1024 — mouse cursor atlas", "bitmap 1280×1024 — atlas de cursores del ratón"),
        [(GameVersion.Runaway3, 113)] = ("bitmap 1280×1024 — cursor shadow mask", "bitmap 1280×1024 — máscara de sombra del cursor"),
        [(GameVersion.Runaway3, 114)] = ("bitmap 1280×1024 — cursor actions atlas", "bitmap 1280×1024 — atlas de acciones del cursor"),
        [(GameVersion.Runaway3, 115)] = ("bitmap 1280×1024 — inventory items sheet 1", "bitmap 1280×1024 — hoja de objetos de inventario 1"),
        [(GameVersion.Runaway3, 116)] = ("bitmap 1280×1024 — inventory items mask 1", "bitmap 1280×1024 — máscara de objetos de inventario 1"),
        [(GameVersion.Runaway3, 117)] = ("bitmap 1280×1024 — inventory items sheet 2", "bitmap 1280×1024 — hoja de objetos de inventario 2"),
        [(GameVersion.Runaway3, 120)] = ("bitmap 1280×1024 — inventory items sheet 3", "bitmap 1280×1024 — hoja de objetos de inventario 3"),
        [(GameVersion.Runaway3, 121)] = ("bitmap 1280×1024 — inventory items mask 3", "bitmap 1280×1024 — máscara de objetos de inventario 3"),
        [(GameVersion.Runaway3, 135)] = ("bitmap 256×256 — UI widget / compass", "bitmap 256×256 — widget de IU / brújula"),
        [(GameVersion.Runaway3, 137)] = ("bitmap 512×512 — dialogue portrait backing", "bitmap 512×512 — fondo de retrato de diálogo"),
        [(GameVersion.Runaway3, 140)] = ("chapter title card — Chapter 1: Brian Basco Is Dead (1280×720)", "pantalla de capítulo — Capítulo 1: Brian Basco ha muerto (1280×720)"),
        [(GameVersion.Runaway3, 141)] = ("chapter title card — Chapter 2: The Art of Running Away (1280×720)", "pantalla de capítulo — Capítulo 2: El arte de escapar (1280×720)"),
        [(GameVersion.Runaway3, 142)] = ("chapter title card — Chapter 3: A Timely Suicide (1280×720)", "pantalla de capítulo — Capítulo 3: Un suicidio bien oportuno (1280×720)"),
        [(GameVersion.Runaway3, 143)] = ("chapter title card — Chapter 4: An Unexpected Ally (1280×720)", "pantalla de capítulo — Capítulo 4: Un aliado inesperado (1280×720)"),
        [(GameVersion.Runaway3, 144)] = ("chapter title card — Chapter 5: Deconstructing Brian (1280×720)", "pantalla de capítulo — Capítulo 5: Brian en todos sus estados (1280×720)"),
        [(GameVersion.Runaway3, 145)] = ("chapter title card — Chapter 6: The End Is Here (1280×720)", "pantalla de capítulo — Capítulo 6: El final está cerca (1280×720)"),
        [(GameVersion.Runaway3, 146)] = ("chapter title card — Epilogue & Flashbacks (1280×720)", "pantalla de capítulo — Capítulo 7: Epílogo y recuerdos (1280×720)"),
        [(GameVersion.Runaway3, 147)] = ("chapter title card — Gina's Story (1280×720)", "pantalla de capítulo — La historia de Gina (1280×720)"),
        [(GameVersion.Runaway3, 148)] = ("chapter title card — Brian's Awakening (1280×720)", "pantalla de capítulo — El despertar de Brian (1280×720)"),
        [(GameVersion.Runaway3, 312)] = ("main menu title backdrop (1280×720)", "fondo de pantalla del menú principal (1280×720)"),
    };

    /// <summary>Returns friendly name for a recognised global asset slot in RESOURCE.000 if known.</summary>
    public static string? GetKnownGlobalAssetName(GameVersion game, int index, string? language = null)
    {
        bool es = IsSpanish(language);
        if (KnownGlobalAssetNames.TryGetValue((game, index), out var pair))
            return es ? pair.Es : pair.En;
        return null;
    }

    private static readonly Dictionary<(string Archive, int Index), (string En, string Es)> KnownSceneAssetNames =
        new(new SceneAssetKeyComparer())
    {
        // Runaway 1: RESOURCE.A00 (Prologue & Introduction)
        [("RESOURCE.A00", 0)] = ("Brian driving car in desert highway", "Brian conduciendo por el desierto"),
        [("RESOURCE.A00", 1)] = ("Brian talking while driving car", "Brian hablando mientras conduce"),
        [("RESOURCE.A00", 2)] = ("car dashboard controls and radio", "mandos y radio del salpicadero del coche"),
        [("RESOURCE.A00", 3)] = ("car headlights illuminating dark highway", "faros del coche iluminando la carretera"),
        [("RESOURCE.A00", 4)] = ("car exterior side view in motion", "vista lateral del coche en marcha"),
        [("RESOURCE.A00", 5)] = ("desert highway perspective at night", "perspectiva de la carretera del desierto de noche"),
        [("RESOURCE.A00", 6)] = ("hospital room background", "fondo de la habitación del hospital"),
        [("RESOURCE.A00", 7)] = ("title card — Chapter 1: Wake Me Before Dying", "pantalla de título — Capítulo 1: Despiértame antes de morir"),
        [("RESOURCE.A00", 8)] = ("title card — Chapter 2: The Mysterious Crucifix", "pantalla de título — Capítulo 2: El extraño crucifijo"),
        [("RESOURCE.A00", 9)] = ("title card — Chapter 3: The Great Escape", "pantalla de título — Capítulo 3: La gran evasión"),
        [("RESOURCE.A00", 10)] = ("title card — Chapter 4: Close Encounters of the Fourth Kind", "pantalla de título — Capítulo 4: Encuentros en la cuarta fase"),
        [("RESOURCE.A00", 11)] = ("title card — Chapter 5: Gifts from the Crypt", "pantalla de título — Capítulo 5: La cripta sagrada"),
        [("RESOURCE.A00", 12)] = ("title card — Chapter 6: The Indian, the Nun and the Finger", "pantalla de título — Capítulo 6: El indio, la monja y el dedo"),

        // Runaway 1: RESOURCE.B02 (Hospital Room)
        [("RESOURCE.B02", 0)] = ("hospital room background", "fondo de la habitación del hospital"),
        [("RESOURCE.B02", 1)] = ("hospital room mask", "máscara de la habitación del hospital"),
        [("RESOURCE.B02", 11)] = ("bathroom door overlay", "capa de la puerta del baño"),
        [("RESOURCE.B02", 12)] = ("door frame occluder overlay", "capa del marco de la puerta"),
        [("RESOURCE.B02", 14)] = ("Gina's bed frame occluder overlay", "capa de la cama de Gina"),
        [("RESOURCE.B02", 15)] = ("privacy curtain occluder overlay", "capa de la cortina separadora"),
        [("RESOURCE.B02", 17)] = ("Gina sleeping in hospital bed", "Gina durmiendo en la cama"),
        [("RESOURCE.B02", 20)] = ("smoke detector LED inactive (off)", "LED detector de humos apagado"),
        [("RESOURCE.B02", 21)] = ("smoke detector LED blinking (on)", "LED detector de humos parpadeando"),
        [("RESOURCE.B02", 22)] = ("window blinds morning light animation", "animación de la luz de la persiana"),

        // Runaway 1: RESOURCE.A01 (Chapter Selection / CD Drive)
        [("RESOURCE.A01", 0)] = ("CD-ROM tray drive animation", "animación de la bandeja de la unidad de CD-ROM"),
        [("RESOURCE.A01", 1)] = ("hospital room background preview", "vista previa de la habitación del hospital"),
        [("RESOURCE.A01", 2)] = ("chapter select card — Chapter 1: Wake Me Before Dying", "tarjeta selector — Capítulo 1: Despiértame antes de morir"),
        [("RESOURCE.A01", 3)] = ("chapter select card — Chapter 2: The Mysterious Crucifix", "tarjeta selector — Capítulo 2: El extraño crucifijo"),
        [("RESOURCE.A01", 4)] = ("chapter select card — Chapter 3: The Great Escape", "tarjeta selector — Capítulo 3: La gran evasión"),
        [("RESOURCE.A01", 5)] = ("chapter select card — Chapter 4: Close Encounters of the Fourth Kind", "tarjeta selector — Capítulo 4: Encuentros en la cuarta fase"),
        [("RESOURCE.A01", 6)] = ("chapter select card — Chapter 5: Gifts from the Crypt", "tarjeta selector — Capítulo 5: La cripta sagrada"),
        [("RESOURCE.A01", 7)] = ("chapter select card — Chapter 6: The Indian, the Nun and the Finger", "tarjeta selector — Capítulo 6: El indio, la monja y el dedo"),

        // Runaway 1: RESOURCE.A02 (Company Logos)
        [("RESOURCE.A02", 0)] = ("Pendulo Studios logo animation (running character)", "animación logotipo de Pendulo Studios (personaje corriendo)"),
        [("RESOURCE.A02", 1)] = ("FX Interactive metallic logo", "logotipo metálico de FX Interactive"),

        // Runaway 1: RESOURCE.B01 (Suburbs & Brian's Departure)
        [("RESOURCE.B01", 0)] = ("suburbs at night panoramic background (1592×600)", "fondo panorámico de los suburbios de noche (1592×600)"),
        [("RESOURCE.B01", 1)] = ("suburbs scene mask", "máscara de escena de los suburbios"),
        [("RESOURCE.B01", 2)] = ("car headlights beam overlay", "capa del haz de los faros del coche"),
        [("RESOURCE.B01", 3)] = ("Brian's convertible car parked at roadside", "descapotable de Brian aparcado en el arcén"),
        [("RESOURCE.B01", 4)] = ("streetlamp light cone overlay", "capa del cono de luz de la farola"),
        [("RESOURCE.B01", 5)] = ("suburban street sign overlay", "capa de la señal de la calle"),
        [("RESOURCE.B01", 6)] = ("night sky & silhouette trees overlay", "capa del cielo nocturno y siluetas de árboles"),
        [("RESOURCE.B01", 7)] = ("suburban house fence overlay", "capa de la valla de la casa"),
        [("RESOURCE.B01", 8)] = ("roadside ditch barrier overlay", "capa de la barrera de la cuneta"),
        [("RESOURCE.B01", 9)] = ("bonus concept art sketch 1 (Gina and thugs)", "boceto de arte conceptual 1 (Gina y matones)"),
        [("RESOURCE.B01", 10)] = ("bonus concept art sketch 2 (desert highway)", "boceto de arte conceptual 2 (carretera del desierto)"),
        [("RESOURCE.B01", 11)] = ("bonus concept art sketch 3 (Mamá Dorita)", "boceto de arte conceptual 3 (Mamá Dorita)"),
        [("RESOURCE.B01", 12)] = ("bonus concept art sketch 4 (Hopi ritual)", "boceto de arte conceptual 4 (ritual hopi)"),
        [("RESOURCE.B01", 13)] = ("bonus concept art sketch 5 (museum lobby)", "boceto de arte conceptual 5 (vestíbulo del museo)"),
        [("RESOURCE.B01", 14)] = ("bonus concept art sketch 6 (Douglasville main street)", "boceto de arte conceptual 6 (calle principal de Douglasville)"),
        [("RESOURCE.B01", 15)] = ("bonus concept art sketch 7 (Saturn and sculpture)", "boceto de arte conceptual 7 (Saturno y escultura)"),
        [("RESOURCE.B01", 16)] = ("bonus concept art sketch 8 (Rutger and weed)", "boceto de arte conceptual 8 (Rutger e invernadero)"),
        [("RESOURCE.B01", 17)] = ("bonus concept art sketch 9 (Sushi's workshop)", "boceto de arte conceptual 9 (taller de Sushi)"),
        [("RESOURCE.B01", 18)] = ("bonus concept art sketch 10 (thugs' cabin)", "boceto de arte conceptual 10 (cabaña de los matones)"),
        [("RESOURCE.B01", 19)] = ("bonus concept art sketch 11 (helicopter in hangar)", "boceto de arte conceptual 11 (helicóptero en hangar)"),
        [("RESOURCE.B01", 20)] = ("bonus concept art sketch 12 (desert graveyard)", "boceto de arte conceptual 12 (cementerio del desierto)"),
        [("RESOURCE.B01", 21)] = ("bonus concept art sketch 13 (hospital interior)", "boceto de arte conceptual 13 (interior del hospital)"),
        [("RESOURCE.B01", 22)] = ("bonus concept art sketch 14 (bank vault)", "boceto de arte conceptual 14 (cámara del banco)"),
        [("RESOURCE.B01", 23)] = ("bonus concept art sketch 15 (steam locomotive)", "boceto de arte conceptual 15 (locomotora de vapor)"),
        [("RESOURCE.B01", 24)] = ("bonus concept art sketch 16 (finale showdown)", "boceto de arte conceptual 16 (desenlace final)"),

        // Runaway 1: RESOURCE.B06 (Hospital Storage Room & Medical Archives)
        [("RESOURCE.B06", 0)] = ("hospital storage room & medical archive background", "fondo del almacén del hospital y archivo médico"),
        [("RESOURCE.B06", 1)] = ("hospital storage room mask", "máscara del almacén del hospital"),
        [("RESOURCE.B06", 2)] = ("medical archive filing cabinet drawers overlay", "capa de los cajones del archivador médico"),
        [("RESOURCE.B06", 3)] = ("cardboard archive box on top shelf overlay", "capa de la caja de cartón del estante superior"),
        [("RESOURCE.B06", 4)] = ("hanging anatomy skeleton model overlay", "capa del modelo anatómico de esqueleto colgado"),
        [("RESOURCE.B06", 5)] = ("storage room fire extinguisher overlay", "capa del extintor de incendios"),
        [("RESOURCE.B06", 6)] = ("escape window to alleyway overlay", "capa de la ventana de escape al callejón"),
        [("RESOURCE.B06", 7)] = ("hospital linen and medicine shelf overlay", "capa de la estantería de sábanas y medicinas"),

        // Runaway 1: RESOURCE.B08 (Hospital Bathroom)
        [("RESOURCE.B08", 0)] = ("hospital bathroom background", "fondo del baño del hospital"),
        [("RESOURCE.B08", 1)] = ("hospital bathroom mask", "máscara del baño del hospital"),
        [("RESOURCE.B08", 2)] = ("bathroom washbasin and taps overlay", "capa del lavabo y grifos del baño"),
        [("RESOURCE.B08", 3)] = ("medicine cabinet mirror overlay", "capa del espejo del botiquín"),
        [("RESOURCE.B08", 4)] = ("medicine cabinet open door with bottles overlay", "capa de la puerta abierta del botiquín con frascos"),
        [("RESOURCE.B08", 5)] = ("bathroom frosted window overlay", "capa de la ventana esmerilada del baño"),
        [("RESOURCE.B08", 6)] = ("bathroom heating radiator overlay", "capa del radiador de calefacción del baño"),
        [("RESOURCE.B08", 7)] = ("bathroom toilet stall door overlay", "capa de la puerta de la cabina del inodoro"),

        // Runaway 1: RESOURCE.D06 (Chicago Museum Main Lobby)
        [("RESOURCE.D06", 0)] = ("museum main lobby panoramic background with dinosaur skeletons (1760×1200)", "fondo panorámico del vestíbulo del museo con esqueletos de dinosaurios (1760×1200)"),
        [("RESOURCE.D06", 1)] = ("museum main lobby mask", "máscara del vestíbulo del museo"),
        [("RESOURCE.D06", 2)] = ("T-Rex dinosaur skeleton skull & ribs overlay", "capa del cráneo y costillas del esqueleto de T-Rex"),
        [("RESOURCE.D06", 3)] = ("Triceratops skeleton display overlay", "capa de la exhibición del esqueleto de Triceratops"),
        [("RESOURCE.D06", 4)] = ("museum mezzanine stairs overlay", "capa de las escaleras al entresuelo del museo"),
        [("RESOURCE.D06", 5)] = ("glass showcase exhibit overlay", "capa de la vitrina de cristal de exhibición"),
        [("RESOURCE.D06", 6)] = ("museum visitor bench overlay", "capa del banco para visitantes del museo"),
        [("RESOURCE.D06", 7)] = ("security surveillance camera overlay", "capa de la cámara de seguridad de vigilancia"),

        // Runaway 1: RESOURCE.D09 (Museum: Mayan Exhibition Hall)
        [("RESOURCE.D09", 0)] = ("Mayan exhibition hall background", "fondo de la sala de la exposición maya"),
        [("RESOURCE.D09", 1)] = ("Mayan exhibition hall mask", "máscara de la sala de la exposición maya"),
        [("RESOURCE.D09", 2)] = ("Mayan pyramid altar offering bowl overlay", "capa del cuenco de ofrendas del altar de la pirámide maya"),
        [("RESOURCE.D09", 3)] = ("Mayan carved stone stela overlay", "capa de la estela maya de piedra tallada"),
        [("RESOURCE.D09", 4)] = ("glass showcase with pre-Columbian gold relics overlay", "capa de la vitrina con reliquias de oro precolombinas"),
        [("RESOURCE.D09", 5)] = ("hall entrance archway overlay", "capa del arco de entrada a la sala"),
        [("RESOURCE.D09", 6)] = ("laser security barrier beams inactive (off)", "haces de barrera de seguridad láser inactivos (apagados)"),
        [("RESOURCE.D09", 7)] = ("laser security barrier beams active (red)", "haces de barrera de seguridad láser activos (rojos)"),

        // Runaway 1: RESOURCE.D10 (Museum: Thermal Analysis Lab)
        [("RESOURCE.D10", 0)] = ("thermal analysis lab background", "fondo del laboratorio de análisis térmico"),
        [("RESOURCE.D10", 1)] = ("thermal analysis lab mask", "máscara del laboratorio de análisis térmico"),
        [("RESOURCE.D10", 2)] = ("thermal imaging scanner chamber overlay", "capa de la cámara de escáner termográfico"),
        [("RESOURCE.D10", 3)] = ("computer monitor displaying Mayan mask thermograph overlay", "capa del monitor con termografía de la máscara maya"),
        [("RESOURCE.D10", 4)] = ("gas tank regulator valves overlay", "capa de las válvulas reguladoras del tanque de gas"),
        [("RESOURCE.D10", 5)] = ("Susan's workstation & laboratory stool overlay", "capa de la mesa de trabajo y taburete de Susan"),
        [("RESOURCE.D10", 6)] = ("fume hood and ventilation ducts overlay", "capa de la campana extractora y conductos"),
        [("RESOURCE.D10", 7)] = ("liquid nitrogen container cylinder overlay", "capa del cilindro de nitrógeno líquido"),

        // Runaway 1: RESOURCE.D14 (ACME Security Keypad & Biometric Scanner)
        [("RESOURCE.D14", 0)] = ("ACME security keypad and scanner panel background", "fondo del panel de teclado y escáner de seguridad ACME"),
        [("RESOURCE.D14", 1)] = ("ACME security keypad mask", "máscara del teclado de seguridad ACME"),
        [("RESOURCE.D14", 2)] = ("keypad numeric button '0'", "botón numérico '0' del teclado"),
        [("RESOURCE.D14", 3)] = ("keypad numeric button '1'", "botón numérico '1' del teclado"),
        [("RESOURCE.D14", 4)] = ("keypad numeric button '2'", "botón numérico '2' del teclado"),
        [("RESOURCE.D14", 5)] = ("keypad numeric button '3'", "botón numérico '3' del teclado"),
        [("RESOURCE.D14", 6)] = ("keypad numeric button '4'", "botón numérico '4' del teclado"),
        [("RESOURCE.D14", 7)] = ("keypad numeric button '5'", "botón numérico '5' del teclado"),
        [("RESOURCE.D14", 8)] = ("keypad numeric button '6'", "botón numérico '6' del teclado"),
        [("RESOURCE.D14", 9)] = ("keypad numeric button '7'", "botón numérico '7' del teclado"),
        [("RESOURCE.D14", 10)] = ("keypad numeric button '8'", "botón numérico '8' del teclado"),
        [("RESOURCE.D14", 11)] = ("keypad numeric button '9'", "botón numérico '9' del teclado"),
        [("RESOURCE.D14", 12)] = ("keypad function button RESET / CLR", "botón de función RESET / CLR del teclado"),
        [("RESOURCE.D14", 13)] = ("keypad function button ENTER / OK", "botón de función ENTER / OK del teclado"),
        [("RESOURCE.D14", 14)] = ("biometric fingerprint glass scanner pad", "lector de cristal de huella dactilar biométrica"),
        [("RESOURCE.D14", 15)] = ("security status LCD display (Access Granted / Denied)", "pantalla LCD de estado de seguridad (Acceso concedido / denegado)"),

        // Runaway 1: RESOURCE.E02 (Thugs' Cabin Exterior)
        [("RESOURCE.E02", 0)] = ("thugs' desert cabin front exterior background", "fondo exterior de la cabaña de los matones en el desierto"),
        [("RESOURCE.E02", 1)] = ("cabin front exterior mask", "máscara exterior de la cabaña"),
        [("RESOURCE.E02", 2)] = ("wooden front porch steps overlay", "capa de los escalones del porche de madera"),
        [("RESOURCE.E02", 3)] = ("rusty pickup truck parked outside overlay", "capa de la camioneta oxidada aparcada fuera"),
        [("RESOURCE.E02", 4)] = ("cabin chimney smoke animation", "animación del humo de la chimenea de la cabaña"),
        [("RESOURCE.E02", 5)] = ("cabin wooden front door overlay", "capa de la puerta de madera de la cabaña"),

        // Runaway 1: RESOURCE.E03 (Thugs' Cabin Main Living Room)
        [("RESOURCE.E03", 0)] = ("thugs' cabin main living room background", "fondo del salón principal de la cabaña de los matones"),
        [("RESOURCE.E03", 1)] = ("cabin living room mask", "máscara del salón de la cabaña"),
        [("RESOURCE.E03", 2)] = ("wooden dining table and chairs overlay", "capa de la mesa de comedor de madera y sillas"),
        [("RESOURCE.E03", 3)] = ("refrigerator with food and beer overlay", "capa del frigorífico con comida y cerveza"),
        [("RESOURCE.E03", 4)] = ("closet door where Gina is tied up overlay", "capa de la puerta del armario donde Gina está atada"),
        [("RESOURCE.E03", 5)] = ("weapon rack with rifles overlay", "capa del armero con rifles"),
        [("RESOURCE.E03", 6)] = ("worn floor rug overlay", "capa de la alfombra desgastada del suelo"),

        // Runaway 1: RESOURCE.E05 (Aircraft Graveyard)
        [("RESOURCE.E05", 0)] = ("aircraft graveyard background", "fondo del cementerio de aviones"),
        [("RESOURCE.E05", 1)] = ("aircraft graveyard mask", "máscara del cementerio de aviones"),
        [("RESOURCE.E05", 2)] = ("crashed fighter jet fuselage overlay", "capa del fuselaje del caza estrellado"),
        [("RESOURCE.E05", 3)] = ("airplane cockpit canopy windshield overlay", "capa de la carlinga de la cabina del avión"),
        [("RESOURCE.E05", 4)] = ("detached jet turbine engine on sand overlay", "capa de la turbina del motor reactor en la arena"),
        [("RESOURCE.E05", 5)] = ("scrap aircraft aluminum debris overlay", "capa de los restos de chatarra de avión"),

        // Runaway 1: RESOURCE.E06 (Drag Queens' Desert Camp & Bus Exterior)
        [("RESOURCE.E06", 0)] = ("desert camp and parked tour bus background", "fondo del campamento en el desierto y autobús de gira"),
        [("RESOURCE.E06", 1)] = ("tour bus exterior mask", "máscara del exterior del autobús"),
        [("RESOURCE.E06", 2)] = ("tour bus side entrance passenger door overlay", "capa de la puerta lateral de pasajeros del autobús"),
        [("RESOURCE.E06", 3)] = ("campfire pit and folding lawn chairs overlay", "capa de la hoguera de campamento y sillas plegables"),
        [("RESOURCE.E06", 4)] = ("diva vanity dressing mirror table on sand overlay", "capa del tocador con espejo de maquillaje en la arena"),
        [("RESOURCE.E06", 5)] = ("sun awning canopy attached to bus overlay", "capa del toldo parasol acoplado al autobús"),

        // Runaway 1: RESOURCE.F09 (Douglasville: Mamá Dorita's House Porch)
        [("RESOURCE.F09", 0)] = ("Mamá Dorita's house front porch background", "fondo del porche de la casa de Mamá Dorita"),
        [("RESOURCE.F09", 1)] = ("Mamá Dorita's porch mask", "máscara del porche de Mamá Dorita"),
        [("RESOURCE.F09", 2)] = ("wooden entrance door overlay", "capa de la puerta de madera de entrada"),
        [("RESOURCE.F09", 3)] = ("porch wooden rocking chair overlay", "capa de la mecedora de madera del porche"),

        // Runaway 1: RESOURCE.F13 (Douglasville: Mamá Dorita's House & Well)
        [("RESOURCE.F13", 0)] = ("Mamá Dorita's house & water well background", "fondo de la casa de Mamá Dorita y pozo de agua"),
        [("RESOURCE.F13", 1)] = ("Mamá Dorita's yard mask", "máscara del patio de Mamá Dorita"),
        [("RESOURCE.F13", 2)] = ("water well with bucket & shrunken head overlay", "capa del pozo de agua con cubo y cabeza reducida"),
        [("RESOURCE.F13", 3)] = ("wooden fence and shrubs overlay", "capa de la valla de madera y arbustos"),

        // Runaway 1: RESOURCE.F18 (Douglasville: Main Street)
        [("RESOURCE.F18", 0)] = ("Douglasville main street panoramic background", "fondo panorámico de la calle principal de Douglasville"),
        [("RESOURCE.F18", 1)] = ("main street mask", "máscara de la calle principal"),
        [("RESOURCE.F18", 2)] = ("saloon porch & swinging batwing doors overlay", "capa del porche del saloon y puertas batientes"),
        [("RESOURCE.F18", 3)] = ("sheriff office wooden sign overlay", "capa del cartel de madera de la oficina del sheriff"),
        [("RESOURCE.F18", 4)] = ("hotel entrance wooden facade overlay", "capa de la fachada de madera de entrada al hotel"),
        [("RESOURCE.F18", 5)] = ("water trough and horse hitching post overlay", "capa del abrevadero y poste para atar caballos"),

        // Runaway 1: RESOURCE.F19 (Douglasville: Ruined Bank)
        [("RESOURCE.F19", 0)] = ("ruined bank interior background", "fondo del interior del banco en ruinas"),
        [("RESOURCE.F19", 1)] = ("ruined bank mask", "máscara del banco en ruinas"),
        [("RESOURCE.F19", 2)] = ("heavy steel vault door overlay", "capa de la pesada puerta de acero de la cámara"),
        [("RESOURCE.F19", 3)] = ("collapsed wooden teller counter overlay", "capa del mostrador de madera derrumbado"),
        [("RESOURCE.F19", 4)] = ("iron teller window security grille overlay", "capa de la reja de hierro de la ventanilla"),
        [("RESOURCE.F19", 5)] = ("rubble and broken roof rafters overlay", "capa de los escombros y vigas rotas del tejado"),

        // Runaway 1: RESOURCE.F20 (Douglasville: Sheriff's Office & Jail)
        [("RESOURCE.F20", 0)] = ("sheriff's office and jail cells background", "fondo de la oficina del sheriff y celdas"),
        [("RESOURCE.F20", 1)] = ("sheriff's office mask", "máscara de la oficina del sheriff"),
        [("RESOURCE.F20", 2)] = ("sheriff's desk with wanted posters overlay", "capa del escritorio del sheriff con carteles de búsqueda"),
        [("RESOURCE.F20", 3)] = ("jail cell iron bars and sliding door overlay", "capa de los barrotes de hierro y puerta corredera de la celda"),
        [("RESOURCE.F20", 4)] = ("cot bed with deceased prisoner skeleton overlay", "capa del catre con el esqueleto del preso"),
        [("RESOURCE.F20", 5)] = ("wall gun rack with Winchester rifles overlay", "capa del armero de pared con rifles Winchester"),

        // Runaway 1: RESOURCE.F26 (Douglasville: Derailed Steam Locomotive \"General\")
        [("RESOURCE.F26", 0)] = ("derailed steam locomotive General background", "fondo de la locomotora de vapor General descarrilada"),
        [("RESOURCE.F26", 1)] = ("locomotive scene mask", "máscara de la locomotora"),
        [("RESOURCE.F26", 2)] = ("locomotive boiler cabin cab door overlay", "capa de la puerta de la cabina de la caldera de la locomotora"),
        [("RESOURCE.F26", 3)] = ("locomotive cowcatcher pilot and driving wheels overlay", "capa del quitapiedras y ruedas motrices de la locomotora"),
        [("RESOURCE.F26", 4)] = ("tender car filled with coal overlay", "capa del vagón ténder lleno de carbón"),
        [("RESOURCE.F26", 5)] = ("locomotive steam whistle and pressure valve animation", "animación del silbato de vapor y válvula de presión"),

        // Runaway 1: RESOURCE.F29 (Douglasville: Joshua's Crater Camp & Synthesizer)
        [("RESOURCE.F29", 0)] = ("Joshua's crater camp & musical synthesizer background", "fondo del campamento en el cráter y sintetizador de Joshua"),
        [("RESOURCE.F29", 1)] = ("Joshua's crater camp mask", "máscara del campamento de Joshua"),
        [("RESOURCE.F29", 2)] = ("extraterrestrial communication synthesizer keyboard overlay", "capa del teclado del sintetizador para comunicación extraterrestre"),
        [("RESOURCE.F29", 3)] = ("synthesizer color keys / sound pipes animation", "animación de los tubos y teclas de colores del sintetizador"),
        [("RESOURCE.F29", 4)] = ("Joshua's motorcycle helmet with telepathic antennas overlay", "capa del casco de moto con antenas telepáticas de Joshua"),
        [("RESOURCE.F29", 5)] = ("camping tent and survival gear overlay", "capa de la tienda de campaña y equipo de supervivencia"),

        // Runaway 1: RESOURCE.F30 (Douglasville: Saturn's Sculptor Studio & Balcony)
        [("RESOURCE.F30", 0)] = ("Saturn's sculptor workshop background", "fondo del taller de escultura de Saturno"),
        [("RESOURCE.F30", 1)] = ("Saturn's workshop mask", "máscara del taller de Saturno"),
        [("RESOURCE.F30", 2)] = ("kinetic metal sculpture in progress overlay", "capa de la escultura cinética de metal en curso"),
        [("RESOURCE.F30", 3)] = ("sculptor workbench with blowtorch & welder overlay", "capa del banco de trabajo con soplete y soldador"),
        [("RESOURCE.F30", 4)] = ("balcony railing overlooking desert street overlay", "capa de la barandilla del balcón hacia la calle del desierto"),
        [("RESOURCE.F30", 5)] = ("paint cans and resin trays overlay", "capa de los botes de pintura y bandejas de resina"),

        // Runaway 1: RESOURCE.F39 (Douglasville: Bank Safe Combination Lock Dial)
        [("RESOURCE.F39", 0)] = ("bank vault combination lock dial background", "fondo del dial de la cerradura de combinación de la caja fuerte"),
        [("RESOURCE.F39", 1)] = ("combination dial mask", "máscara del dial de combinación"),
        [("RESOURCE.F39", 2)] = ("rotating circular combination dial (0-99)", "dial circular giratorio de combinación (0-99)"),
        [("RESOURCE.F39", 3)] = ("vault lock handle locking bolt mechanism overlay", "capa del mecanismo de cerrojos y manivela de la caja fuerte"),

        // Runaway 1: RESOURCE.H38 (Douglasville: Sushi's Hacker Loft & Workshop)
        [("RESOURCE.H38", 0)] = ("Sushi's hacker computer loft background", "fondo del loft informático y taller de Sushi"),
        [("RESOURCE.H38", 1)] = ("Sushi's loft mask", "máscara del loft de Sushi"),
        [("RESOURCE.H38", 2)] = ("multi-monitor workstation desk overlay", "capa de la mesa de trabajo con múltiples monitores"),
        [("RESOURCE.H38", 3)] = ("computer screens glowing telemetry animation", "animación de los monitores con telemetría brillante"),
        [("RESOURCE.H38", 4)] = ("satellite receiver decoder dish overlay", "capa de la antena decodificadora por satélite"),
        [("RESOURCE.H38", 5)] = ("leather sofa and electronics workbench overlay", "capa del sofá de cuero y banco de componentes electrónicos"),

        // Runaway 1: RESOURCE.H40 (Military Camp: Command Tent & Surveillance Terminal)
        [("RESOURCE.H40", 0)] = ("Colonel Kordsmeier's command tent interior background", "fondo del interior de la tienda de mando del coronel Kordsmeier"),
        [("RESOURCE.H40", 1)] = ("command tent mask", "máscara de la tienda de mando"),
        [("RESOURCE.H40", 2)] = ("military surveillance terminal screen overlay", "capa de la pantalla del terminal de vigilancia militar"),
        [("RESOURCE.H40", 3)] = ("tactical operations planning map table overlay", "capa de la mesa con mapa de operaciones tácticas"),
        [("RESOURCE.H40", 4)] = ("radio communications transceiver rack overlay", "capa del equipo de radiocomunicaciones"),
        [("RESOURCE.H40", 5)] = ("tent entrance flap canvas overlay", "capa de la lona de entrada a la tienda"),
    };

    private sealed class SceneAssetKeyComparer : IEqualityComparer<(string Archive, int Index)>
    {
        public bool Equals((string Archive, int Index) x, (string Archive, int Index) y) =>
            string.Equals(x.Archive, y.Archive, StringComparison.OrdinalIgnoreCase) && x.Index == y.Index;

        public int GetHashCode((string Archive, int Index) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Archive), obj.Index);
    }

    /// <summary>Returns friendly name for an identified scene archive asset if known.</summary>
    public static string? GetKnownSceneAssetName(string archiveName, int index, string? language = null)
    {
        string baseName = Path.GetFileName(archiveName);
        bool es = IsSpanish(language);
        if (KnownSceneAssetNames.TryGetValue((baseName, index), out var pair))
            return es ? pair.Es : pair.En;
        return null;
    }

    /// <summary>Formats a scene entry label in the requested language with readable name first and entry ID after.</summary>
    public static string FormatSceneEntryLabel(FsNode entry, string? language = null)
    {
        bool es = IsSpanish(language);
        ImageInfo? img = entry.Image;

        // Check if a specific investigated name exists for this scene entry:
        if (entry.ArchivePath is not null)
        {
            string? specific = GetKnownSceneAssetName(entry.ArchivePath, entry.EntryIndex, language);
            if (specific is not null)
            {
                if (img is not null)
                {
                    if (entry.Kind == EntryKind.Animation || entry.Kind == EntryKind.CharacterAnimation)
                    {
                        string frameWord = es
                            ? (img.Frames == 1 ? "fotograma" : "fotogramas")
                            : (img.Frames == 1 ? "frame" : "frames");
                        return $"{specific}, {img.Frames} {frameWord}, {img.Width}×{img.Height} ({entry.Name})";
                    }
                    if (entry.Kind == EntryKind.Overlay && (img.X != 0 || img.Y != 0))
                    {
                        string at = es ? "en" : "at";
                        return $"{specific} {img.Width}×{img.Height} {at} {img.X},{img.Y} ({entry.Name})";
                    }
                    return $"{specific} {img.Width}×{img.Height} ({entry.Name})";
                }
                return $"{specific} ({entry.Name})";
            }
        }

        switch (entry.Kind)
        {
            case EntryKind.Background when img is not null:
            {
                if (entry.ArchivePath is not null && Path.GetFileName(entry.ArchivePath).Equals("RESOURCE.A03", StringComparison.OrdinalIgnoreCase) && img.Width == 204 && img.Height == 120)
                {
                    string chLabel = entry.EntryIndex switch
                    {
                        0 => es ? "miniatura capítulo 1 (prólogo)" : "chapter 1 (prologue) thumbnail",
                        _ => es ? $"miniatura capítulo {entry.EntryIndex + 1}" : $"chapter {entry.EntryIndex + 1} thumbnail"
                    };
                    return $"{chLabel} {img.Width}×{img.Height} ({entry.Name})";
                }
                if (entry.ArchivePath is not null && Path.GetFileName(entry.ArchivePath).Equals("RESOURCE.I04", StringComparison.OrdinalIgnoreCase) && img.Width == 200 && img.Height == 93)
                {
                    string tileLabel = es ? $"miniatura de destino {entry.EntryIndex - 1}" : $"travel destination tile {entry.EntryIndex - 1}";
                    return $"{tileLabel} {img.Width}×{img.Height} ({entry.Name})";
                }
                string desc = es ? (entry.EntryIndex == 0 ? "fondo de escena" : "fondo alternativo") : "background";
                string mask = img.IsMask ? (es ? " (máscara)" : " (mask)") : "";
                return $"{desc} {img.Width}×{img.Height}{mask} ({entry.Name})";
            }

            case EntryKind.Mask when img is not null:
            {
                string desc = es ? "máscara de escena" : "scene mask";
                return $"{desc} {img.Width}×{img.Height} ({entry.Name})";
            }

            case EntryKind.Overlay when img is not null:
            {
                if (img.X == 0 && img.Y == 0)
                {
                    string spriteDesc = es ? "capa" : "sprite";
                    return $"{spriteDesc} {img.Width}×{img.Height} ({entry.Name})";
                }
                string desc = es ? "capa" : "overlay";
                string at = es ? "en" : "at";
                return $"{desc} {img.Width}×{img.Height} {at} {img.X},{img.Y} ({entry.Name})";
            }

            case EntryKind.Animation when img is not null:
            {
                string desc = es ? "animación" : "animation";
                string frameWord = es
                    ? (img.Frames == 1 ? "fotograma" : "fotogramas")
                    : (img.Frames == 1 ? "frame" : "frames");
                return $"{desc}, {img.Frames} {frameWord}, {img.Width}×{img.Height} ({entry.Name})";
            }

            case EntryKind.CharacterAnimation when img is not null:
            {
                string desc = es ? "personaje" : "character animation";
                string frameWord = es
                    ? (img.Frames == 1 ? "fotograma" : "fotogramas")
                    : (img.Frames == 1 ? "frame" : "frames");
                return $"{desc}, {img.Frames} {frameWord}, {img.Width}×{img.Height} ({entry.Name})";
            }

            case EntryKind.Dialogue:
            {
                string desc = es ? "frase" : "phrase";
                return string.IsNullOrEmpty(entry.Subtitle)
                    ? $"{desc} ({entry.Name})"
                    : $"{desc} ({entry.Name})  \"{entry.Subtitle}\"";
            }

            default:
            {
                if (entry.Size == 1536)
                {
                    string attrWord = es ? "tabla de atributos de escena" : "scene attribute table";
                    return $"{attrWord} ({entry.Name})";
                }
                if (entry.Size == 1792)
                {
                    string lookupWord = es ? "páginas de atributos de escena (7 páginas)" : "scene attribute lookup pages (7 pages)";
                    return $"{lookupWord} ({entry.Name})";
                }
                if (entry.Size is >= 14_006 and <= 65_536 && (entry.EntryIndex == 4 || entry.Name.EndsWith("e04", StringComparison.OrdinalIgnoreCase)))
                {
                    string metaWord = es ? "metadatos y acciones de escena" : "scene metadata & interactions";
                    return $"{metaWord} ({entry.Name})";
                }
                if (entry.Size == 335_529)
                {
                    string navWord = es ? "malla de navegación" : "navigation mesh";
                    return $"{navWord} ({entry.Name})";
                }
                if (entry.Size == 43_659)
                {
                    string routeWord = es ? "malla de rutas" : "route mesh";
                    return $"{routeWord} ({entry.Name})";
                }
                if (entry.Size == 704)
                {
                    string corridorWord = es ? "corredor de perspectiva" : "perspective corridor table";
                    return $"{corridorWord} ({entry.Name})";
                }
                if (entry.Size == RouteDecoder.TotalSizeTnbtWaypoints)
                {
                    string navWord = es ? "tabla de navegación" : "waypoint navigation table";
                    return $"{navWord} ({entry.Name})";
                }
                if (entry.Size == RouteDecoder.TotalSizeTnbtHops)
                {
                    string hopWord = es ? "tabla de transiciones de zona" : "zone transition table";
                    return $"{hopWord} ({entry.Name})";
                }
                string dataWord = es ? "datos" : "data";
                return $"{dataWord}, {VirtualFileSystem.FormatSize(entry.Size)} ({entry.Name})";
            }
        }
    }
}
