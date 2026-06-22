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

    replace_once(
        package / "utils.py",
        "    Glib = auto()\n"
        "    Oodle = auto()\n",
        "    Gzip = auto()\n"
        "    Oodle = auto()\n"
        "    LZ4 = auto()\n"
        "    Zstd = auto()\n",
    )
    replace_once(
        package / "entry.py",
        "import logging\n"
        "import zlib\n"
        "import io\n",
        "import logging\n"
        "import gzip\n"
        "import zlib\n"
        "import io\n"
        "from lz4.block import decompress as lz4_decompress\n"
        "from zstandard import ZstdDecompressor\n",
    )
    replace_once(
        package / "entry.py",
        "        elif self.compression == COMPRESSION.Oodle:\n",
        "        elif self.compression == COMPRESSION.Gzip:\n"
        "            for r in ranges:\n"
        "                decompressed_data.write(gzip.decompress(data[r.start : r.stop]))\n"
        "            return decompressed_data.getvalue()\n"
        "\n"
        "        elif self.compression == COMPRESSION.LZ4:\n"
        "            total_uncompressed = self.size\n"
        "            offset = 0\n"
        "            for r in ranges:\n"
        "                expected = min(chunk_size, total_uncompressed - offset)\n"
        "                decompressed_data.write(lz4_decompress(data[r.start : r.stop], uncompressed_size=expected))\n"
        "                offset += expected\n"
        "            return decompressed_data.getvalue()\n"
        "\n"
        "        elif self.compression == COMPRESSION.Zstd:\n"
        "            total_uncompressed = self.size\n"
        "            offset = 0\n"
        "            decompressor = ZstdDecompressor()\n"
        "            for r in ranges:\n"
        "                expected = min(chunk_size, total_uncompressed - offset)\n"
        "                decompressed_data.write(decompressor.decompress(data[r.start : r.stop], max_output_size=expected))\n"
        "                offset += expected\n"
        "            return decompressed_data.getvalue()\n"
        "\n"
        "        elif self.compression == COMPRESSION.Oodle:\n",
    )


if __name__ == "__main__":
    main()
