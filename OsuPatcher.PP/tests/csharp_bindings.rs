use interoptopus::{Interop, Error};
use interoptopus_backend_csharp::{Generator, Config};

#[test]
fn bindings_cs() -> Result<(), Error> {
    Generator::new(
        Config {
            dll_name: "refx_ffi".to_string(),
            ..Config::default()
        },
        refx_ffi::my_inventory(),
    )
    .write_file("bindings/refx_ffi.cs")?;

    Ok(())
}
