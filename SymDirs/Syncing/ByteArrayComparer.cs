namespace SymDirs.Syncing;

public class ByteArrayComparer : IEqualityComparer<byte[]>
{
    public bool Equals(byte[]? left, byte[]? right) {
        if (left == null && right == null)
            return true;
        if (left == null || right == null)
            return false;
        return left.SequenceEqual(right);
    }
    
    public int GetHashCode(byte[] data) {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        
        // https://stackoverflow.com/questions/16340/how-do-i-generate-a-hashcode-from-a-byte-array-in-c
        // Apparently this is a good way to generate a hash code for a byte array.
        // "Modified FNV Hash in C#"
        // The unchecked keyword kinda worries me but should be safe in this context.
        const int p = 16777619;
        int hash = unchecked((int)2166136261);

        for (int i = 0; i < data.Length; i++)
            hash = (hash ^ data[i]) * p;

        return hash;
    }
}