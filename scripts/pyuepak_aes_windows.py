import ctypes
from ctypes import wintypes


_bcrypt = ctypes.WinDLL("bcrypt")
_status = wintypes.LONG
_handle = wintypes.HANDLE
_byte_pointer = ctypes.POINTER(ctypes.c_ubyte)

_bcrypt.BCryptOpenAlgorithmProvider.argtypes = [
    ctypes.POINTER(_handle),
    wintypes.LPCWSTR,
    wintypes.LPCWSTR,
    wintypes.DWORD,
]
_bcrypt.BCryptOpenAlgorithmProvider.restype = _status
_bcrypt.BCryptCloseAlgorithmProvider.argtypes = [_handle, wintypes.DWORD]
_bcrypt.BCryptCloseAlgorithmProvider.restype = _status
_bcrypt.BCryptGetProperty.argtypes = [
    _handle,
    wintypes.LPCWSTR,
    _byte_pointer,
    wintypes.DWORD,
    ctypes.POINTER(wintypes.DWORD),
    wintypes.DWORD,
]
_bcrypt.BCryptGetProperty.restype = _status
_bcrypt.BCryptSetProperty.argtypes = [
    _handle,
    wintypes.LPCWSTR,
    _byte_pointer,
    wintypes.DWORD,
    wintypes.DWORD,
]
_bcrypt.BCryptSetProperty.restype = _status
_bcrypt.BCryptGenerateSymmetricKey.argtypes = [
    _handle,
    ctypes.POINTER(_handle),
    _byte_pointer,
    wintypes.DWORD,
    _byte_pointer,
    wintypes.DWORD,
    wintypes.DWORD,
]
_bcrypt.BCryptGenerateSymmetricKey.restype = _status
_bcrypt.BCryptDestroyKey.argtypes = [_handle]
_bcrypt.BCryptDestroyKey.restype = _status
_bcrypt.BCryptDecrypt.argtypes = [
    _handle,
    _byte_pointer,
    wintypes.DWORD,
    ctypes.c_void_p,
    _byte_pointer,
    wintypes.DWORD,
    _byte_pointer,
    wintypes.DWORD,
    ctypes.POINTER(wintypes.DWORD),
    wintypes.DWORD,
]
_bcrypt.BCryptDecrypt.restype = _status


def _check(status, operation):
    if status != 0:
        raise OSError("{} failed with NTSTATUS 0x{:08X}".format(operation, status & 0xFFFFFFFF))


def _buffer(value):
    return (ctypes.c_ubyte * len(value)).from_buffer_copy(value)


def _get_dword_property(handle, name):
    value = wintypes.DWORD()
    written = wintypes.DWORD()
    _check(
        _bcrypt.BCryptGetProperty(
            handle,
            name,
            ctypes.cast(ctypes.byref(value), _byte_pointer),
            ctypes.sizeof(value),
            ctypes.byref(written),
            0,
        ),
        "BCryptGetProperty",
    )
    return value.value


def aes_ecb_decrypt(key, data):
    if len(key) not in (16, 24, 32):
        raise ValueError("AES key must contain 16, 24 or 32 bytes.")
    if len(data) % 16:
        raise ValueError("AES ECB data length must be a multiple of 16 bytes.")
    if not data:
        return b""

    algorithm = _handle()
    key_handle = _handle()
    _check(_bcrypt.BCryptOpenAlgorithmProvider(ctypes.byref(algorithm), "AES", None, 0), "BCryptOpenAlgorithmProvider")
    try:
        mode = "ChainingModeECB\0".encode("utf-16le")
        mode_buffer = _buffer(mode)
        _check(
            _bcrypt.BCryptSetProperty(
                algorithm,
                "ChainingMode",
                mode_buffer,
                len(mode),
                0,
            ),
            "BCryptSetProperty",
        )
        key_object = (ctypes.c_ubyte * _get_dword_property(algorithm, "ObjectLength"))()
        key_buffer = _buffer(key)
        _check(
            _bcrypt.BCryptGenerateSymmetricKey(
                algorithm,
                ctypes.byref(key_handle),
                key_object,
                len(key_object),
                key_buffer,
                len(key),
                0,
            ),
            "BCryptGenerateSymmetricKey",
        )
        try:
            input_buffer = _buffer(data)
            output_buffer = (ctypes.c_ubyte * len(data))()
            written = wintypes.DWORD()
            _check(
                _bcrypt.BCryptDecrypt(
                    key_handle,
                    input_buffer,
                    len(data),
                    None,
                    None,
                    0,
                    output_buffer,
                    len(output_buffer),
                    ctypes.byref(written),
                    0,
                ),
                "BCryptDecrypt",
            )
            return bytes(output_buffer[:written.value])
        finally:
            _bcrypt.BCryptDestroyKey(key_handle)
    finally:
        _bcrypt.BCryptCloseAlgorithmProvider(algorithm, 0)
