use akatsuki_pp_1_0_1::{ // bancho.py server usually uses this version
    osu_2019::{stars::OsuPerformanceAttributes as Akatsuki101OsuPerfAttrs, OsuPP as Akatsuki101OsuPP},
    AnyPP as Akatsuki101AnyPP, Beatmap as Akatsuki101Beatmap, GameMode as Akatsuki101GameMode,
    PerformanceAttributes as Akatsuki101PerfAttrs,
};
use akatsuki_pp_1_1_2::{
    any::PerformanceAttributes as Akatsuki112PerfAttrs,
    model::mode::GameMode as Akatsuki112GameMode,
    osu_2019::{stars::OsuPerformanceAttributes as Akatsuki112OsuPerfAttrs, OsuPP as Akatsuki112OsuPP},
    Beatmap as Akatsuki112Beatmap,
};
use refx_pp::{
    any::PerformanceAttributes as RefxPerfAttrs,
    model::mode::GameMode as RefxGameMode,
    Beatmap as RefxBeatmap,
};
use interoptopus::{
    extra_type, ffi_function, ffi_type, function,
    patterns::{option::FFIOption, slice::FFISlice},
    Inventory, InventoryBuilder,
};

#[ffi_type]
#[repr(C)]
#[derive(Clone, Default, PartialEq)]
pub struct CalculatePerformanceResult {
    pub pp: f64,
    pub stars: f64,
}

impl std::fmt::Display for CalculatePerformanceResult {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "pp: {}, stars: {}", self.pp, self.stars)
    }
}

trait IntoResult {
    fn into_result(self) -> CalculatePerformanceResult;
}

impl IntoResult for Akatsuki101PerfAttrs {
    fn into_result(self) -> CalculatePerformanceResult {
        CalculatePerformanceResult { pp: self.pp(), stars: self.stars() }
    }
}

impl IntoResult for Akatsuki101OsuPerfAttrs {
    fn into_result(self) -> CalculatePerformanceResult {
        CalculatePerformanceResult { pp: self.pp, stars: self.difficulty.stars }
    }
}

impl IntoResult for Akatsuki112PerfAttrs {
    fn into_result(self) -> CalculatePerformanceResult {
        CalculatePerformanceResult { pp: self.pp(), stars: self.stars() }
    }
}

impl IntoResult for Akatsuki112OsuPerfAttrs {
    fn into_result(self) -> CalculatePerformanceResult {
        CalculatePerformanceResult { pp: self.pp, stars: self.difficulty.stars }
    }
}

impl IntoResult for RefxPerfAttrs {
    fn into_result(self) -> CalculatePerformanceResult {
        CalculatePerformanceResult { pp: self.pp(), stars: self.stars() }
    }
}

#[ffi_function]
#[no_mangle]
pub unsafe extern "C" fn calculate_akatsuki_101_from_bytes(
    beatmap_bytes: FFISlice<u8>,
    mode: u32,
    mods: u32,
    max_combo: u32,
    accuracy: f32,
    miss_count: u32,
    passed_objects: FFIOption<u32>,
) -> CalculatePerformanceResult {
    let beatmap = Akatsuki101Beatmap::from_bytes(beatmap_bytes.as_slice()).unwrap();
    let passed_objects = passed_objects.into_option();

    if mode == 0 && (mods & 128) != 0 {
        let mut calc = Akatsuki101OsuPP::new(&beatmap)
            .mods(mods)
            .combo(max_combo as usize)
            .misses(miss_count as usize);

        if let Some(passed) = passed_objects {
            calc = calc.passed_objects(passed as usize);
        }

        calc = calc.accuracy(accuracy);

        calc.calculate().into_result()
    } else {
        let game_mode = match mode {
            0 => Akatsuki101GameMode::Osu,
            1 => Akatsuki101GameMode::Taiko,
            2 => Akatsuki101GameMode::Catch,
            3 => Akatsuki101GameMode::Mania,
            _ => panic!("Invalid mode: {}", mode),
        };

        let mut calc = Akatsuki101AnyPP::new(&beatmap)
            .mode(game_mode)
            .mods(mods)
            .combo(max_combo as usize)
            .n_misses(miss_count as usize)
            .accuracy(accuracy as f64);

        if let Some(passed) = passed_objects {
            calc = calc.passed_objects(passed as usize);
        }

        calc.calculate().into_result()
    }
}

#[ffi_function]
#[no_mangle]
pub unsafe extern "C" fn calculate_akatsuki_112_from_bytes(
    beatmap_bytes: FFISlice<u8>,
    mode: u32,
    mods: u32,
    max_combo: u32,
    accuracy: f32,
    miss_count: u32,
    passed_objects: FFIOption<u32>,
) -> CalculatePerformanceResult {
    let beatmap = Akatsuki112Beatmap::from_bytes(beatmap_bytes.as_slice()).unwrap();
    let passed_objects = passed_objects.into_option();

    if mode == 0 && (mods & 128) != 0 {
        let mut calc = Akatsuki112OsuPP::from_map(&beatmap)
            .mods(mods)
            .combo(max_combo)
            .misses(miss_count);

        if let Some(passed) = passed_objects {
            calc = calc.passed_objects(passed);
        }

        calc = calc.accuracy(accuracy);

        calc.calculate().into_result()
    } else {
        let game_mode = match mode {
            0 => Akatsuki112GameMode::Osu,
            1 => Akatsuki112GameMode::Taiko,
            2 => Akatsuki112GameMode::Catch,
            3 => Akatsuki112GameMode::Mania,
            _ => panic!("Invalid mode: {}", mode),
        };

        let mut calc = beatmap
            .performance()
            .try_mode(game_mode)
            .unwrap()
            .mods(mods)
            .lazer(false)
            .combo(max_combo)
            .misses(miss_count)
            .accuracy(accuracy as f64);

        if let Some(passed) = passed_objects {
            calc = calc.passed_objects(passed);
        }

        calc.calculate().into_result()
    }
}

#[ffi_function]
#[no_mangle]
pub unsafe extern "C" fn calculate_refx_from_bytes(
    beatmap_bytes: FFISlice<u8>,
    mode: u32,
    mods: u32,
    max_combo: u32,
    accuracy: f32,
    miss_count: u32,
    legacy_score: i64,
    passed_objects: FFIOption<u32>,
) -> CalculatePerformanceResult {
    let beatmap = RefxBeatmap::from_bytes(beatmap_bytes.as_slice()).unwrap();

    let game_mode = match mode {
        0 => RefxGameMode::Osu,
        1 => RefxGameMode::Taiko,
        2 => RefxGameMode::Catch,
        3 => RefxGameMode::Mania,
        _ => panic!("Invalid mode: {}", mode),
    };

    let mut calc = beatmap
        .performance()
        .try_mode(game_mode)
        .unwrap()
        .mods(mods)
        .lazer(false)
        .combo(max_combo)
        .misses(miss_count)
        .legacy_total_score(legacy_score)
        .accuracy(accuracy as f64);

    if let Some(passed) = passed_objects.into_option() {
        calc = calc.passed_objects(passed);
    }

    calc.calculate().into_result()
}

pub fn my_inventory() -> Inventory {
    InventoryBuilder::new()
        .register(extra_type!(CalculatePerformanceResult))
        .register(function!(calculate_akatsuki_101_from_bytes))
        .register(function!(calculate_akatsuki_112_from_bytes))
        .register(function!(calculate_refx_from_bytes))
        .inventory()
}
