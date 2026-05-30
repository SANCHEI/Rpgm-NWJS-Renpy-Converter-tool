class OodleUnavailable(RuntimeError):
    pass


class OfflineOodle:
    def compress(self, *_args, **_kwargs):
        raise OodleUnavailable("Oodle-compressed Unreal PAK archives are not supported by the offline build.")

    def decompress(self, *_args, **_kwargs):
        raise OodleUnavailable("Oodle-compressed Unreal PAK archives are not supported by the offline build.")


_oodle = OfflineOodle()


def oodle():
    return _oodle
