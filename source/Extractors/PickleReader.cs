using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class PickleReader
    {
        public static object Load(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new InvalidDataException("Pickle payload is empty.");
            Parser parser = new Parser(data);
            object value = parser.Parse();
            return value;
        }

        private sealed class Parser
        {
            private readonly byte[] data;
            private int position;
            private readonly List<object> stack = new List<object>();
            private readonly List<int> marks = new List<int>();
            private readonly List<object> memo = new List<object>();

            public Parser(byte[] data)
            {
                this.data = data;
                this.position = 0;
            }

            public object Parse()
            {
                while (true)
                {
                    if (position >= data.Length)
                        throw new InvalidDataException("Pickle stream ended without STOP.");
                    byte opcode = data[position++];
                    switch (opcode)
                    {
                        case 0x80: ReadByte(); break; // PROTO
                        case 0x95: position += 8; break; // FRAME
                        case (byte)'(': marks.Add(stack.Count); break; // MARK
                        case (byte)'0': Pop(); break; // POP
                        case (byte)'1': PopToMark(true); break; // POP_MARK
                        case (byte)'2': stack.Add(stack[stack.Count - 1]); break; // DUP
                        case (byte)'N': stack.Add(null); break; // NONE
                        case 0x88: stack.Add(true); break; // NEWTRUE
                        case 0x89: stack.Add(false); break; // NEWFALSE
                        case (byte)'J': stack.Add((long)ReadInt32()); break; // BININT
                        case (byte)'K': stack.Add((long)ReadByte()); break; // BININT1
                        case (byte)'M': stack.Add((long)ReadUInt16()); break; // BININT2
                        case 0x8A: stack.Add(ReadLong(1)); break; // LONG1
                        case 0x8B: stack.Add(ReadLong(4)); break; // LONG4
                        case (byte)'I': stack.Add(ParseIntLine()); break; // INT
                        case (byte)'L': stack.Add(ParseLongLine()); break; // LONG
                        case (byte)'F': stack.Add(ParseFloatLine()); break; // FLOAT
                        case (byte)'S': stack.Add(ParseQuotedString()); break; // STRING -> bytes
                        case (byte)'U': stack.Add(ReadBytes(ReadByte())); break; // SHORT_BINSTRING
                        case (byte)'T': stack.Add(ReadBytes(ReadInt32())); break; // BINSTRING
                        case (byte)'C': stack.Add(ReadBytes(ReadByte())); break; // SHORT_BINBYTES
                        case (byte)'B': stack.Add(ReadBytes(ReadInt32())); break; // BINBYTES
                        case 0x8D: stack.Add(ReadBytes((int)ReadUInt64())); break; // BINBYTES8
                        case (byte)'V': stack.Add(ParseRawUnicode()); break; // UNICODE -> string
                        case (byte)'X': stack.Add(ReadUtf8(ReadInt32())); break; // BINUNICODE
                        case 0x8C: stack.Add(ReadUtf8(ReadByte())); break; // SHORT_BINUNICODE
                        case 0x8E:
                            {
                                ulong wide = ReadUInt64();
                                if (wide > int.MaxValue)
                                    throw new InvalidDataException("Pickle string is too large.");
                                stack.Add(ReadUtf8((int)wide)); // BINUNICODE8
                                break;
                            }
                        case (byte)'}': stack.Add(new Dictionary<object, object>()); break; // EMPTY_DICT
                        case (byte)']': stack.Add(new List<object>()); break; // EMPTY_LIST
                        case (byte)')': stack.Add(new List<object>()); break; // EMPTY_TUPLE as list
                        case (byte)'d': BuildDict(); break; // DICT
                        case (byte)'l': BuildList(); break; // LIST
                        case (byte)'t': stack.Add(PopToMark(false)); break; // TUPLE as list
                        case 0x85: stack.Add(SingletonList(Pop())); break; // TUPLE1
                        case 0x86: stack.Add(PairList()); break; // TUPLE2
                        case 0x87: stack.Add(TripleList()); break; // TUPLE3
                        case (byte)'s': SetItem(); break; // SETITEM
                        case (byte)'u': SetItems(); break; // SETITEMS
                        case (byte)'a': Append(); break; // APPEND
                        case (byte)'e': AppendMany(); break; // APPENDS
                        case (byte)'q': Memoize(ReadByte()); break; // BINPUT
                        case (byte)'h': stack.Add(GetMemo(ReadByte())); break; // BINGET
                        case (byte)'j': stack.Add(GetMemo(ReadInt32())); break; // LONG_BINGET
                        case (byte)'p': Memoize(ParseDecimalLine()); break; // PUT
                        case (byte)'g': stack.Add(GetMemo(ParseDecimalLine())); break; // GET
                        case 0x94: Memoize(memo.Count); break; // MEMOIZE
                        case (byte)'.':
                            if (stack.Count == 0)
                                throw new InvalidDataException("Pickle STOP with empty stack.");
                            return stack[stack.Count - 1];
                        default:
                            throw new InvalidDataException("Unsupported pickle opcode 0x" + opcode.ToString("X2") + " at offset " + (position - 1) + ".");
                    }
                }
            }

            private object Pop()
            {
                if (stack.Count == 0)
                    throw new InvalidDataException("Pickle stack underflow.");
                object value = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                return value;
            }

            private List<object> PopToMark(bool discard)
            {
                if (marks.Count == 0)
                    throw new InvalidDataException("Pickle mark stack underflow.");
                int mark = marks[marks.Count - 1];
                marks.RemoveAt(marks.Count - 1);
                if (mark > stack.Count)
                    throw new InvalidDataException("Pickle mark is out of range.");
                List<object> items = stack.GetRange(mark, stack.Count - mark);
                stack.RemoveRange(mark, stack.Count - mark);
                if (discard) return null;
                return items;
            }

            private void Memoize(int index)
            {
                if (stack.Count == 0)
                    throw new InvalidDataException("Pickle memoize with empty stack.");
                while (memo.Count <= index) memo.Add(null);
                memo[index] = stack[stack.Count - 1];
            }

            private object GetMemo(int index)
            {
                if (index < 0 || index >= memo.Count || memo[index] == null)
                    throw new InvalidDataException("Pickle memo reference is out of range.");
                return memo[index];
            }

            private static List<object> SingletonList(object value)
            {
                return new List<object>(new object[] { value });
            }

            private List<object> PairList()
            {
                object second = Pop();
                object first = Pop();
                return new List<object>(new object[] { first, second });
            }

            private List<object> TripleList()
            {
                object third = Pop();
                object second = Pop();
                object first = Pop();
                return new List<object>(new object[] { first, second, third });
            }

            private void BuildDict()
            {
                List<object> items = PopToMark(false);
                if (items.Count % 2 != 0)
                    throw new InvalidDataException("Pickle DICT has an odd item count.");
                Dictionary<object, object> result = new Dictionary<object, object>();
                for (int i = 0; i < items.Count; i += 2)
                    result[items[i]] = items[i + 1];
                stack.Add(result);
            }

            private void BuildList()
            {
                stack.Add(PopToMark(false));
            }

            private void SetItem()
            {
                object value = Pop();
                object key = Pop();
                object target = Pop();
                Dictionary<object, object> dictionary = target as Dictionary<object, object>;
                if (dictionary == null)
                    throw new InvalidDataException("Pickle SETITEM target is not a dict.");
                dictionary[key] = value;
                stack.Add(dictionary);
            }

            private void SetItems()
            {
                List<object> items = PopToMark(false);
                object target = Pop();
                Dictionary<object, object> dictionary = target as Dictionary<object, object>;
                if (dictionary == null)
                    throw new InvalidDataException("Pickle SETITEMS target is not a dict.");
                if (items.Count % 2 != 0)
                    throw new InvalidDataException("Pickle SETITEMS has an odd item count.");
                for (int i = 0; i < items.Count; i += 2)
                    dictionary[items[i]] = items[i + 1];
                stack.Add(dictionary);
            }

            private void Append()
            {
                object value = Pop();
                object target = Pop();
                List<object> list = target as List<object>;
                if (list == null)
                    throw new InvalidDataException("Pickle APPEND target is not a list.");
                list.Add(value);
                stack.Add(list);
            }

            private void AppendMany()
            {
                List<object> items = PopToMark(false);
                object target = Pop();
                List<object> list = target as List<object>;
                if (list == null)
                    throw new InvalidDataException("Pickle APPENDS target is not a list.");
                list.AddRange(items);
                stack.Add(list);
            }

            private long ReadLong(int sizeLength)
            {
                int count;
                if (sizeLength == 1)
                {
                    count = ReadByte();
                }
                else
                {
                    count = ReadInt32();
                    if (count < 0)
                        throw new InvalidDataException("Pickle long has a negative size.");
                }
                if (position + count > data.Length)
                    throw new InvalidDataException("Pickle long is truncated.");
                ulong magnitude = 0;
                for (int i = 0; i < count; i++)
                    magnitude |= ((ulong)data[position + i]) << (8 * i);
                position += count;
                if (count > 0 && (data[position - 1] & 0x80) != 0 && count < 8)
                    return (long)(magnitude | (~0UL << (8 * count)));
                if (count >= 8 && (data[position - 1] & 0x80) != 0)
                    throw new InvalidDataException("Pickle long does not fit into 64 bits.");
                return (long)magnitude;
            }

            private int ParseDecimalLine()
            {
                int value = 0;
                bool any = false;
                while (true)
                {
                    if (position >= data.Length)
                        throw new InvalidDataException("Pickle decimal is truncated.");
                    byte b = data[position++];
                    if (b == (byte)'\n') break;
                    if (b < (byte)'0' || b > (byte)'9')
                        throw new InvalidDataException("Pickle decimal is not numeric.");
                    any = true;
                    value = checked(value * 10 + (b - (byte)'0'));
                }
                if (!any)
                    throw new InvalidDataException("Pickle decimal is empty.");
                return value;
            }

            private byte ReadByte()
            {
                if (position >= data.Length)
                    throw new InvalidDataException("Pickle stream is truncated.");
                return data[position++];
            }

            private int ReadInt32()
            {
                if (position + 4 > data.Length)
                    throw new InvalidDataException("Pickle stream is truncated.");
                int value = data[position] | (data[position + 1] << 8) | (data[position + 2] << 16) | (data[position + 3] << 24);
                position += 4;
                return value;
            }

            private int ReadUInt16()
            {
                if (position + 2 > data.Length)
                    throw new InvalidDataException("Pickle stream is truncated.");
                int value = data[position] | (data[position + 1] << 8);
                position += 2;
                return value;
            }

            private ulong ReadUInt64()
            {
                if (position + 8 > data.Length)
                    throw new InvalidDataException("Pickle stream is truncated.");
                ulong value = 0;
                for (int i = 0; i < 8; i++)
                    value |= ((ulong)data[position + i]) << (8 * i);
                position += 8;
                return value;
            }

            private byte[] ReadBytes(int count)
            {
                if (count < 0 || position + count > data.Length)
                    throw new InvalidDataException("Pickle string is truncated.");
                byte[] value = new byte[count];
                Buffer.BlockCopy(data, position, value, 0, count);
                position += count;
                return value;
            }

            private string ReadUtf8(int count)
            {
                return Encoding.UTF8.GetString(ReadBytes(count));
            }

            private long ParseIntLine()
            {
                string text = ReadLine().Trim();
                if (text.EndsWith("L", StringComparison.Ordinal))
                    text = text.Substring(0, text.Length - 1);
                return long.Parse(text, CultureInfo.InvariantCulture);
            }

            private long ParseLongLine()
            {
                string text = ReadLine().Trim();
                if (text.EndsWith("L", StringComparison.Ordinal) || text.EndsWith("l", StringComparison.Ordinal))
                    text = text.Substring(0, text.Length - 1);
                return long.Parse(text, CultureInfo.InvariantCulture);
            }

            private double ParseFloatLine()
            {
                return double.Parse(ReadLine().Trim(), CultureInfo.InvariantCulture);
            }

            private string ReadLine()
            {
                int start = position;
                while (position < data.Length && data[position] != (byte)'\n') position++;
                if (position >= data.Length)
                    throw new InvalidDataException("Pickle line is truncated.");
                string line = Encoding.ASCII.GetString(data, start, position - start);
                position++;
                return line;
            }

            private byte[] ParseQuotedString()
            {
                string text = ReadLine().Trim();
                if (text.Length < 2)
                    throw new InvalidDataException("Pickle string literal is truncated.");
                char quote = text[0];
                if ((quote != '\'' && quote != '"') || text[text.Length - 1] != quote)
                    throw new InvalidDataException("Pickle string literal is not quoted.");
                StringBuilder output = new StringBuilder();
                for (int i = 1; i + 1 < text.Length;)
                {
                    char c = text[i];
                    if (c != '\\')
                    {
                        output.Append(c);
                        i++;
                        continue;
                    }
                    i++;
                    if (i + 1 > text.Length)
                        throw new InvalidDataException("Pickle string escape is truncated.");
                    char e = text[i];
                    switch (e)
                    {
                        case 'n': output.Append('\n'); i++; break;
                        case 't': output.Append('\t'); i++; break;
                        case 'r': output.Append('\r'); i++; break;
                        case '\\': output.Append('\\'); i++; break;
                        case '\'': output.Append('\''); i++; break;
                        case '"': output.Append('"'); i++; break;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                            {
                                int digits = 0;
                                int value = 0;
                                while (digits < 3 && i < text.Length - 1 && text[i] >= '0' && text[i] <= '7')
                                {
                                    value = value * 8 + (text[i] - '0');
                                    i++;
                                    digits++;
                                }
                                output.Append((char)value);
                                break;
                            }
                        case 'x':
                            output.Append((char)(ParseHex(text, i + 1, 2)));
                            i += 3;
                            break;
                        case 'u':
                            output.Append((char)ParseHex(text, i + 1, 4));
                            i += 5;
                            break;
                        case 'U':
                            {
                                int code = ParseHex(text, i + 1, 8);
                                output.Append(char.ConvertFromUtf32(code));
                                i += 9;
                                break;
                            }
                        default:
                            throw new InvalidDataException("Unsupported pickle string escape \\" + e + ".");
                    }
                }
                // encoding="bytes" semantics: 8-bit literals become raw bytes.
                byte[] result = new byte[output.Length];
                for (int i = 0; i < output.Length; i++)
                {
                    char c = output[i];
                    if (c > 0xFF)
                        throw new InvalidDataException("Pickle string literal is not bytes-compatible.");
                    result[i] = (byte)c;
                }
                return result;
            }

            private string ParseRawUnicode()
            {
                string text = ReadLine();
                StringBuilder output = new StringBuilder();
                for (int i = 0; i < text.Length;)
                {
                    char c = text[i];
                    if (c != '\\')
                    {
                        output.Append(c);
                        i++;
                        continue;
                    }
                    i++;
                    if (i >= text.Length)
                        throw new InvalidDataException("Pickle unicode escape is truncated.");
                    char e = text[i];
                    if (e == 'u')
                    {
                        output.Append((char)ParseHex(text, i + 1, 4));
                        i += 5;
                    }
                    else if (e == 'U')
                    {
                        output.Append(char.ConvertFromUtf32(ParseHex(text, i + 1, 8)));
                        i += 9;
                    }
                    else if (e == 'n') { output.Append('\n'); i++; }
                    else if (e == 't') { output.Append('\t'); i++; }
                    else if (e == 'r') { output.Append('\r'); i++; }
                    else if (e == '\\') { output.Append('\\'); i++; }
                    else
                    {
                        output.Append(c);
                        output.Append(e);
                        i++;
                    }
                }
                return output.ToString();
            }

            private int ParseHex(string text, int offset, int digits)
            {
                if (offset < 0 || offset + digits > text.Length)
                    throw new InvalidDataException("Pickle hex escape is truncated.");
                int value = 0;
                for (int i = 0; i < digits; i++)
                {
                    char c = text[offset + i];
                    int digit;
                    if (c >= '0' && c <= '9') digit = c - '0';
                    else if (c >= 'a' && c <= 'f') digit = c - 'a' + 10;
                    else if (c >= 'A' && c <= 'F') digit = c - 'A' + 10;
                    else throw new InvalidDataException("Pickle hex escape is not hexadecimal.");
                    value = value * 16 + digit;
                }
                return value;
            }
        }
    }
}
