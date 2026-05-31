import ctypes
import glob
import os


class OodleUnavailable(RuntimeError):
    pass


class OfflineOodle:
    def __init__(self, reason):
        self.reason = reason

    def compress(self, *_args, **_kwargs):
        raise OodleUnavailable(self.reason)

    def decompress(self, *_args, **_kwargs):
        raise OodleUnavailable(self.reason)


class LocalOodle:
    def __init__(self, path):
        self.path = path
        self.library = ctypes.WinDLL(path)
        self.decompress_function = self.library.OodleLZ_Decompress
        self.decompress_function.restype = ctypes.c_longlong

    def compress(self, *_args, **_kwargs):
        raise OodleUnavailable("Oodle compression is not used by Game Asset Tool.")

    def decompress(self, source, expected_size):
        source_buffer = ctypes.create_string_buffer(source)
        destination = ctypes.create_string_buffer(expected_size)
        result = self.decompress_function(
            source_buffer,
            len(source),
            destination,
            expected_size,
            1,
            0,
            0,
            None,
            0,
            None,
            None,
            None,
            0,
            3,
        )
        if result != expected_size:
            raise OodleUnavailable(
                "Local Oodle decoder returned {} bytes instead of {}.".format(result, expected_size)
            )
        return destination.raw


def _candidates():
    explicit = os.environ.get("OODLE_DLL", "").strip()
    if explicit:
        yield explicit

    game_path = os.environ.get("GAME_PATH", "").strip()
    if game_path and os.path.isdir(game_path):
        for path in glob.glob(os.path.join(game_path, "**", "oo2core*_win64.dll"), recursive=True):
            yield path

    program_files = os.environ.get("ProgramFiles", r"C:\Program Files")
    pattern = os.path.join(
        program_files,
        "Epic Games",
        "UE_*",
        "Engine",
        "Binaries",
        "ThirdParty",
        "Oodle",
        "Win64",
        "oo2core*_win64.dll",
    )
    for path in glob.glob(pattern):
        yield path


def _load():
    for candidate in _candidates():
        if not os.path.isfile(candidate):
            continue
        try:
            return LocalOodle(candidate)
        except (OSError, AttributeError):
            continue
    return OfflineOodle(
        "Oodle-compressed Unreal PAK detected, but no local oo2core*_win64.dll was found "
        "inside the game or an installed Unreal Engine. Game Asset Tool does not redistribute "
        "or silently download Epic's proprietary Oodle decoder."
    )


_oodle = _load()


def oodle():
    return _oodle
