use oozextract::Extractor;
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::slice;

#[no_mangle]
pub unsafe extern "C" fn gat_oodle_decompress(
    source: *const u8,
    source_size: usize,
    destination: *mut u8,
    destination_size: usize,
) -> isize {
    if source.is_null() || destination.is_null() || source_size == 0 || destination_size == 0 {
        return -1;
    }

    catch_unwind(AssertUnwindSafe(|| {
        let input = slice::from_raw_parts(source, source_size);
        let output = slice::from_raw_parts_mut(destination, destination_size);
        match Extractor::new().read_from_slice(input, output) {
            Ok(written) if written == destination_size => written as isize,
            Ok(_) => -2,
            Err(_) => -3,
        }
    }))
    .unwrap_or(-4)
}
