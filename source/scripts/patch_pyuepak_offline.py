import pathlib
import sys


def replace_once(path, old, new):
    text = path.read_text(encoding="utf-8").replace("\r\n", "\n")
    if text.count(old) != 1:
        raise RuntimeError("Unexpected pyuepak source in {}".format(path))
    path.write_text(text.replace(old, new), encoding="utf-8", newline="\n")


def main():
    package = pathlib.Path(sys.argv[1]) / "pyuepak"

    replace_once(
        package / "entry.py",
        "from cryptography.hazmat.backends import default_backend\n"
        "from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes\n",
        "from .aes_windows import aes_ecb_decrypt\n",
    )
    replace_once(
        package / "entry.py",
        "            cipher = Cipher(algorithms.AES(key), modes.ECB(), backend=default_backend())\n"
        "            decryptor = cipher.decryptor()\n"
        "\n"
        "            decrypted_data = bytearray()\n"
        "            for i in range(0, len(data), 16):\n"
        "                block = data[i : i + 16]\n"
        "                if len(block) == 16:  # Only decrypt full blocks\n"
        "                    decrypted_block = decryptor.update(block)\n"
        "                    decrypted_data.extend(decrypted_block)\n"
        "                else:\n"
        "                    decrypted_data.extend(block)\n"
        "\n"
        "            # Truncate to original compressed size\n"
        "            data = decrypted_data[: self.compressed_size]\n",
        "            data = aes_ecb_decrypt(key, data)[: self.compressed_size]\n",
    )

    replace_once(
        package / "index.py",
        "from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes\n"
        "from cryptography.hazmat.backends import default_backend\n",
        "from .aes_windows import aes_ecb_decrypt\n",
    )
    replace_once(
        package / "index.py",
        "    cipher = Cipher(algorithms.AES(key), modes.ECB(), backend=default_backend())\n"
        "    decryptor = cipher.decryptor()\n"
        "\n"
        "    return decryptor.update(data) + decryptor.finalize()\n",
        "    return aes_ecb_decrypt(key, data)\n",
    )

    for line in (
        'fh = logging.FileHandler("spam.log")\n',
        "fh.setFormatter(formatter)\n",
        "logger.addHandler(fh)\n",
    ):
        replace_once(package / "pak.py", line, "")


if __name__ == "__main__":
    main()
