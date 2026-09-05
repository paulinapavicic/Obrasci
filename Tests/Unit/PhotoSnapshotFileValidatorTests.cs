using System.Text;
using FluentAssertions;
using Obrasci.Services;
using Xunit;

namespace Tests.Unit
{
    public class PhotoSnapshotFileValidatorTests
    {
        [Fact]
        public async Task ValidateJsonSnapshotAsync_with_valid_json_object_does_not_throw()
        {
            await using var stream = CreateStream(
                """{"name":"example","count":1}""");

            Func<Task> act = () =>
                PhotoSnapshotFileValidator.ValidateJsonSnapshotAsync(stream);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task ValidateJsonSnapshotAsync_with_valid_json_array_does_not_throw()
        {
            await using var stream = CreateStream(
                """[{"id":1},{"id":2}]""");

            Func<Task> act = () =>
                PhotoSnapshotFileValidator.ValidateJsonSnapshotAsync(stream);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task ValidateJsonSnapshotAsync_with_json_object_after_whitespace_does_not_throw()
        {
            await using var stream = CreateStream(
                "   {\"name\":\"example\"}");

            Func<Task> act = () =>
                PhotoSnapshotFileValidator.ValidateJsonSnapshotAsync(stream);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task ValidateJsonSnapshotAsync_with_unreadable_stream_throws_invalid_operation_exception()
        {
            await using var stream = new NonReadableMemoryStream(
                Encoding.UTF8.GetBytes("""{"name":"example"}"""));

            Func<Task> act = () =>
                PhotoSnapshotFileValidator.ValidateJsonSnapshotAsync(stream);

            await act.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("The uploaded file cannot be read.");
        }

        [Fact]
        public async Task ValidateJsonSnapshotAsync_with_empty_file_throws_invalid_data_exception()
        {
            await using var stream = new MemoryStream();

            Func<Task> act = () =>
                PhotoSnapshotFileValidator.ValidateJsonSnapshotAsync(stream);

            await act.Should()
                .ThrowAsync<InvalidDataException>()
                .WithMessage("The uploaded file is empty.");
        }

        [Fact]
        public async Task ValidateJsonSnapshotAsync_with_file_larger_than_one_mb_throws_invalid_data_exception()
        {
            var tooLargeBytes = new byte[1_048_577];
            tooLargeBytes[0] = (byte)'{';

            await using var stream = new MemoryStream(tooLargeBytes);

            Func<Task> act = () =>
                PhotoSnapshotFileValidator.ValidateJsonSnapshotAsync(stream);

            await act.Should()
                .ThrowAsync<InvalidDataException>()
                .WithMessage("The uploaded file exceeds the 1 MB size limit.");
        }

        [Fact]
        public async Task ValidateJsonSnapshotAsync_when_header_cannot_be_fully_read_throws_invalid_data_exception()
        {
            await using var stream = new ShortReadStream(
                Encoding.UTF8.GetBytes("""{"name":"example"}"""));

            Func<Task> act = () =>
                PhotoSnapshotFileValidator.ValidateJsonSnapshotAsync(stream);

            await act.Should()
                .ThrowAsync<InvalidDataException>()
                .WithMessage("Unable to read the file header.");
        }

        [Theory]
        [InlineData("hello")]
        [InlineData("\"just a json string\"")]
        [InlineData("123")]
        [InlineData("true")]
        [InlineData("null")]
        public async Task ValidateJsonSnapshotAsync_when_root_is_not_object_or_array_throws_invalid_data_exception(
            string content)
        {
            await using var stream = CreateStream(content);

            Func<Task> act = () =>
                PhotoSnapshotFileValidator.ValidateJsonSnapshotAsync(stream);

            await act.Should()
                .ThrowAsync<InvalidDataException>()
                .WithMessage("The uploaded file is not a JSON object or array.");
        }

        [Theory]
        [InlineData("{")]
        [InlineData("[")]
        [InlineData("""{"name": }""")]
        [InlineData("""[1,]""")]
        public async Task ValidateJsonSnapshotAsync_with_malformed_json_throws_invalid_data_exception(
            string content)
        {
            await using var stream = CreateStream(content);

            Func<Task> act = () =>
                PhotoSnapshotFileValidator.ValidateJsonSnapshotAsync(stream);

            await act.Should()
                .ThrowAsync<InvalidDataException>()
                .WithMessage("The uploaded file contains invalid JSON.");
        }

        private static MemoryStream CreateStream(string content)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        private sealed class NonReadableMemoryStream : MemoryStream
        {
            public NonReadableMemoryStream(byte[] bytes)
                : base(bytes)
            {
            }

            public override bool CanRead => false;
        }

        private sealed class ShortReadStream : MemoryStream
        {
            public ShortReadStream(byte[] bytes)
                : base(bytes)
            {
            }

            public override ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                if (Position >= Length)
                {
                    return ValueTask.FromResult(0);
                }

                var bytesToReturn = Math.Min(1, buffer.Length);

                buffer.Span[0] = ReadByte() is var value && value >= 0
                    ? (byte)value
                    : (byte)0;

                return ValueTask.FromResult(bytesToReturn);
            }
        }
    }
}