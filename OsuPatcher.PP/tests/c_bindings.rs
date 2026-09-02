use interoptopus::{Interop, Error};
use interoptopus_backend_c::{Generator, Config};

#[test]
fn bindings_c() -> Result<(), Error> {
    Generator::new(
        Config {
            ifndef: "refx_ffi".to_string(),
            ..Config::default()
        },
        refx_ffi::my_inventory(),
    )
    .write_file("bindings/refx_ffi.h")?;

    Ok(())
}
