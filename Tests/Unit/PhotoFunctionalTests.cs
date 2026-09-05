using FluentAssertions;
using Obrasci.Models;
using Obrasci.Services.Functional;
using Xunit;

namespace Tests.Unit
{
    public class PhotoFunctionalTests
    {
        [Fact]
        public void FilterBy_returns_only_photos_matching_predicate()
        {
            
            var photos = new[]
            {
                new Photo
                {
                    FileName = "small.jpg",
                    SizeBytes = 50
                },
                new Photo
                {
                    FileName = "large.jpg",
                    SizeBytes = 500
                },
                new Photo
                {
                    FileName = "medium.jpg",
                    SizeBytes = 150
                }
            };

        
            var result = PhotoFunctional
                .FilterBy(photos, photo => photo.SizeBytes >= 150)
                .ToList();

           
            result.Select(photo => photo.FileName)
                .Should()
                .Equal("large.jpg", "medium.jpg");
        }

        [Fact]
        public void FilterBy_when_no_photo_matches_returns_empty_collection()
        {
           
            var photos = new[]
            {
                new Photo { SizeBytes = 10 },
                new Photo { SizeBytes = 20 }
            };

          
            var result = PhotoFunctional
                .FilterBy(photos, photo => photo.SizeBytes > 100)
                .ToList();

          
            result.Should().BeEmpty();
        }

        [Fact]
        public void ToFileNames_returns_file_names_in_original_order()
        {
        
            var photos = new[]
            {
                new Photo { FileName = "first.jpg" },
                new Photo { FileName = "second.png" },
                new Photo { FileName = "third.webp" }
            };

            var result = PhotoFunctional.ToFileNames(photos);

          
            result.Should().Equal(
                "first.jpg",
                "second.png",
                "third.webp");
        }

        [Fact]
        public void ToFileNames_with_empty_collection_returns_empty_collection()
        {
            
            var result = PhotoFunctional.ToFileNames(Array.Empty<Photo>());

           
            result.Should().BeEmpty();
        }

        [Fact]
        public void TotalBytes_sums_file_sizes()
        {
            
            var photos = new[]
            {
                new Photo { SizeBytes = 100 },
                new Photo { SizeBytes = 250 },
                new Photo { SizeBytes = 10 }
            };

            
            var result = PhotoFunctional.TotalBytes(photos);

           
            result.Should().Be(360);
        }

        [Fact]
        public void TotalBytes_with_empty_collection_returns_zero()
        {
            
            var result = PhotoFunctional.TotalBytes(Array.Empty<Photo>());

           
            result.Should().Be(0);
        }

        [Fact]
        public void And_returns_true_only_when_both_predicates_are_true()
        {
         
            Func<Photo, bool> big = photo => photo.SizeBytes > 100;
            Func<Photo, bool> jpeg = photo => photo.ContentType == "image/jpeg";

            var combined = PhotoFunctional.And(big, jpeg);

           
            combined(new Photo
            {
                SizeBytes = 500,
                ContentType = "image/jpeg"
            }).Should().BeTrue();

            combined(new Photo
            {
                SizeBytes = 500,
                ContentType = "image/png"
            }).Should().BeFalse();

            combined(new Photo
            {
                SizeBytes = 50,
                ContentType = "image/jpeg"
            }).Should().BeFalse();
        }

        [Fact]
        public void And_short_circuits_second_predicate_when_first_predicate_is_false()
        {
          
            var secondPredicateWasCalled = false;

            Func<Photo, bool> first = _ => false;

            Func<Photo, bool> second = _ =>
            {
                secondPredicateWasCalled = true;
                return true;
            };

            var combined = PhotoFunctional.And(first, second);

           
            var result = combined(new Photo());

            result.Should().BeFalse();
            secondPredicateWasCalled.Should().BeFalse();
        }

        [Fact]
        public void ParseHashtags_handles_csv_space_and_hash_separators()
        {
         
            var raw = "#Sunset, beach mountain  #beach";

         
            var tags = PhotoFunctional.ParseHashtags(raw);

           
            tags.Should().BeEquivalentTo(
                new[] { "sunset", "beach", "mountain" });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseHashtags_with_null_empty_or_whitespace_returns_empty_list(
            string? raw)
        {
          
            var tags = PhotoFunctional.ParseHashtags(raw);

         
            tags.Should().BeEmpty();
        }

        [Fact]
        public void ParseHashtags_trims_lowercases_and_removes_duplicates()
        {
            var raw = " Sunset , sunset,  BEACH #beach  #Travel ";

        
            var tags = PhotoFunctional.ParseHashtags(raw);

           
            tags.Should().Equal("sunset", "beach", "travel");
        }

        [Fact]
        public void DailyLimitFor_returns_expected_limit_for_each_package()
        {
         
            PhotoFunctional.DailyLimitFor(PackageType.Free)
                .Should().Be(5);

            PhotoFunctional.DailyLimitFor(PackageType.Pro)
                .Should().Be(20);

            PhotoFunctional.DailyLimitFor(PackageType.Gold)
                .Should().Be(100);
        }

        [Fact]
        public void CountByAuthor_counts_photos_for_each_named_author()
        {
           
            var ana = new ApplicationUser
            {
                UserName = "ana"
            };

            var ivan = new ApplicationUser
            {
                UserName = "ivan"
            };

            var photos = new[]
            {
                new Photo { User = ana },
                new Photo { User = ana },
                new Photo { User = ivan }
            };

           
            var result = PhotoFunctional.CountByAuthor(photos);

        
            result.Should().BeEquivalentTo(new Dictionary<string, int>
            {
                ["ana"] = 2,
                ["ivan"] = 1
            });
        }

        [Fact]
        public void CountByAuthor_groups_missing_user_or_username_as_unknown()
        {
           
            var userWithoutName = new ApplicationUser
            {
                UserName = null
            };

            var photos = new[]
            {
                new Photo { User = null },
                new Photo { User = userWithoutName },
                new Photo { User = null }
            };

          
            var result = PhotoFunctional.CountByAuthor(photos);

           
            result.Should().ContainSingle();
            result["unknown"].Should().Be(3);
        }

        [Fact]
        public void CountByAuthor_with_empty_collection_returns_empty_dictionary()
        {
            var result = PhotoFunctional.CountByAuthor(
                Array.Empty<Photo>());

            
            result.Should().BeEmpty();
        }
    }
}