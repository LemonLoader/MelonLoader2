//! Console interaction like windows is not possible on Unix systems.

pub unsafe fn init() -> Result<(), DynErr> {
    Ok(())
}

#[allow(dead_code)]
pub fn set_title(title: &str) {

}

pub fn set_handles() -> Result<(), DynErr> {
    Ok(())
}

pub fn null_handles() -> Result<(), DynErr> {
    Ok(())
}
