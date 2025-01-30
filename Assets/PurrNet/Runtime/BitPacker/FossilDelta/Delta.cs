using System;
using PurrNet.Packing;

namespace Fossil
{
	public static class Delta
	{
		public const ushort NHASH = 16;

		static readonly RollingHash _rollingHash = new RollingHash();
		
		public static void Create(ReadOnlySpan<byte> origin, ReadOnlySpan<byte> target, BitPacker zDelta) {
			int i, lastRead = -1;
			
			var originLength = origin.Length;
			var targetLength = target.Length;

			Packer<int>.Write(zDelta, targetLength);
			Packer<char>.Write(zDelta, '\n');

			// If the source is very small, it means that we have no
			// chance of ever doing a copy command.  Just output a single
			// literal segment for the entire target and exit.
			if (originLength <= NHASH) {
				Packer<int>.Write(zDelta, targetLength);
				Packer<char>.Write(zDelta, ':');
				zDelta.WriteBytes(target);
				Packer<int>.Write(zDelta, (int)Checksum(target));
				Packer<char>.Write(zDelta, ';');
				return;
			}

			// Compute the hash table used to locate matching sections in the source.
			int nHash = originLength / NHASH;
			int[] collide =  new int[nHash];
			int[] landmark = new int[nHash];
			for (i = 0; i < collide.Length; i++) collide[i] = -1;
			for (i = 0; i < landmark.Length; i++) landmark[i] = -1;
			int hv;
			
			for (i = 0; i < originLength-NHASH; i += NHASH) {
				_rollingHash.Init(origin, i);
				hv = (int) (_rollingHash.Value() % nHash);
				collide[i/NHASH] = landmark[hv];
				landmark[hv] = i/NHASH;
			}

			int _base = 0;
			int iSrc, iBlock;
			int bestCnt, bestOfst, bestLitsz;
			while (_base+NHASH<targetLength) {
				bestOfst=0;
				bestLitsz=0;
				_rollingHash.Init(target, _base);
				i = 0; // Trying to match a landmark against zOut[_base+i]
				bestCnt = 0;
				while (true) {
					int limit = 250;
					hv = (int) (_rollingHash.Value() % nHash);
					iBlock = landmark[hv];
					while (iBlock >= 0 && (limit--)>0 ) {
						//
						// The hash window has identified a potential match against
						// landmark block iBlock.  But we need to investigate further.
						//
						// Look for a region in zOut that matches zSrc. Anchor the search
						// at zSrc[iSrc] and zOut[_base+i].  Do not include anything prior to
						// zOut[_base] or after zOut[outLen] nor anything after zSrc[srcLen].
						//
						// Set cnt equal to the length of the match and set ofst so that
						// zSrc[ofst] is the first element of the match.  litsz is the number
						// of characters between zOut[_base] and the beginning of the match.
						// sz will be the overhead (in bytes) needed to encode the copy
						// command.  Only generate copy command if the overhead of the
						// copy command is less than the amount of literal text to be copied.
						//
						int cnt, litsz;
						int j, k, x, y;

						// Beginning at iSrc, match forwards as far as we can.
						// j counts the number of characters that match.
						iSrc = iBlock*NHASH;
						for (j = 0, x = iSrc, y = _base+i; x < originLength && y < targetLength; j++, x++, y++) {
							if (origin[x] != target[y]) break;
						}
						j--;

						// Beginning at iSrc-1, match backwards as far as we can.
						// k counts the number of characters that match.
						for (k = 1; k < iSrc && k <= i; k++) {
							if (origin[iSrc-k] != target[_base+i-k]) break;
						}
						k--;

						// Compute the offset and size of the matching region.
						cnt = j+k+1;
						litsz = i-k;  // Number of bytes of literal text before the copy
						// sz will hold the number of bytes needed to encode the "insert"
						// command and the copy command, not counting the "insert" text.
						
						// Fixed overhead in bytes:
						// - 4 bytes for each integer (i-k, cnt, ofst)
						// - 1 byte for each command character (':', '@', ',')
						const int COMMAND_OVERHEAD = 4 * 3 + 3;
						if (cnt >= COMMAND_OVERHEAD && cnt > bestCnt) {
							// Remember this match only if it is the best so far and it
							// does not increase the file size.
							bestCnt = cnt;
							bestOfst = iSrc-k;
							bestLitsz = litsz;
						}

						// Check the next matching block
						iBlock = collide[iBlock];
					}

					// We have a copy command that does not cause the delta to be larger
					// than a literal insert.  So add the copy command to the delta.
					if (bestCnt > 0) {
						if (bestLitsz > 0) {
							// Add an insert command before the copy.
							Packer<int>.Write(zDelta, bestLitsz);
							Packer<char>.Write(zDelta, ':');
							zDelta.WriteBytes(target.Slice(_base, bestLitsz));
							_base += bestLitsz;
						}
						_base += bestCnt;
						Packer<int>.Write(zDelta, bestCnt);
						Packer<char>.Write(zDelta, '@');
						Packer<int>.Write(zDelta, bestOfst);
						Packer<char>.Write(zDelta, ',');
						
						if (bestOfst + bestCnt -1 > lastRead) {
							lastRead = bestOfst + bestCnt - 1;
						}
						break;
					}

					// If we reach this point, it means no match is found so far
					if (_base+i+NHASH >= targetLength){
						// We have reached the end and have not found any
						// matches.  Do an "insert" for everything that does not match
						Packer<int>.Write(zDelta, targetLength-_base);
						Packer<char>.Write(zDelta, ':');
						zDelta.WriteBytes(target.Slice(_base, targetLength-_base));
						_base = targetLength;
						break;
					}

					// Advance the hash by one character. Keep looking for a match.
					_rollingHash.Next(target[_base+i+NHASH]);
					i++;
				}
			}
			// Output a final "insert" record to get all the text at the end of
			// the file that does not match anything in the source.
			if(_base < targetLength) {
				Packer<int>.Write(zDelta, targetLength-_base);
				Packer<char>.Write(zDelta, ':');
				zDelta.WriteBytes(target.Slice(_base, targetLength-_base));
			}
			// Output the final checksum record.
			Packer<int>.Write(zDelta, (int)Checksum(target));
			Packer<char>.Write(zDelta, ';');
		}

		public static void Apply(ReadOnlySpan<byte> origin, BitPacker delta, ReadOnlySpan<byte> deltaRaw, BitPacker zOut) {
			int limit = 0, total = 0;
			uint lenSrc = (uint) origin.Length;
			uint lenDelta = (uint) deltaRaw.Length;
			Packer<int>.Read(delta, ref limit);
			
			if (delta.ReadChar() != '\n')
				throw new Exception("size integer not terminated by \'\\n\'");
			
			while(delta.positionInBytes < lenDelta) {
				int cnt = 0, ofst = 0;
				
				Packer<int>.Read(delta, ref cnt);

				switch (delta.ReadChar()) {
				case '@':
					Packer<int>.Read(delta, ref ofst);

					if (delta.positionInBytes < lenDelta && delta.ReadChar() != ',')
						throw new Exception("copy command not terminated by \',\'");
					total += cnt;
					if (total > limit)
						throw new Exception("copy exceeds output file size");
					if (ofst+cnt > lenSrc)
						throw new Exception("copy extends past end of input");
					zOut.WriteBytes(origin.Slice(ofst, cnt));
					break;

				case ':':
					total += cnt;
					if (total > limit)
						throw new Exception("insert command gives an output larger than predicted");
					if (cnt > lenDelta)
						throw new Exception("insert count exceeds size of delta");
					
					var deltapos = delta.positionInBytes;
					zOut.WriteBytes(deltaRaw.Slice(deltapos, cnt));
					delta.SkipBytes(cnt);
					break;

				case ';':
					/*if (cnt != Checksum(zOut.ToByteData().span))
						throw new Exception("bad checksum");*/
					if (total != limit)
						throw new Exception("generated size does not match predicted size");
					return;

				default:
					throw new Exception("unknown delta operator");
				}
			}
			throw new Exception("unterminated delta");
		}

		// Return a 32-bit checksum of the array.
		static uint Checksum(ReadOnlySpan<byte> arr, int count = 0, uint sum = 0) {
			uint sum0 = 0, sum1 = 0, sum2 = 0, N = (uint) (count == 0 ? arr.Length : count);

			int z = 0;

			while(N >= 16){
				sum0 += (uint) arr[z+0] + arr[z+4] + arr[z+8]  + arr[z+12];
				sum1 += (uint) arr[z+1] + arr[z+5] + arr[z+9]  + arr[z+13];
				sum2 += (uint) arr[z+2] + arr[z+6] + arr[z+10] + arr[z+14];
				sum  += (uint) arr[z+3] + arr[z+7] + arr[z+11] + arr[z+15];
				z += 16;
				N -= 16;
			}
			while(N >= 4){
				sum0 += arr[z+0];
				sum1 += arr[z+1];
				sum2 += arr[z+2];
				sum  += arr[z+3];
				z += 4;
				N -= 4;
			}

			sum += (sum2 << 8) + (sum1 << 16) + (sum0 << 24);
			switch (N&3) {
			case 3:
				sum += (uint) (arr [z + 2] << 8);
				sum += (uint) (arr [z + 1] << 16);
				sum += (uint) (arr [z + 0] << 24);
				break;
			case 2:
				sum += (uint) (arr [z + 1] << 16);
				sum += (uint) (arr [z + 0] << 24);
				break;
			case 1:
				sum += (uint) (arr [z + 0] << 24);
				break;
			}
			return sum;
		}

	}
}