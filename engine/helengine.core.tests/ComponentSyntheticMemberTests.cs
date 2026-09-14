using System.Reflection;
using helengine;
using Xunit;

namespace helengine.core.tests {
    /// <summary>
    /// Verifies the synthetic member bags on <see cref="Component"/> keep their public contract while staying unallocated until a value is stored.
    /// </summary>
    public sealed class ComponentSyntheticMemberTests {
        [Fact]
        public void Synthetic_member_bags_are_not_allocated_until_a_value_is_set() {
            Component component = new Component();

            Assert.Null(ReadSyntheticBag(component, "SyntheticStringMembers"));
            Assert.Null(ReadSyntheticBag(component, "SyntheticBooleanMembers"));
            Assert.Null(ReadSyntheticBag(component, "SyntheticInt32Members"));
            Assert.Null(ReadSyntheticBag(component, "SyntheticSingleMembers"));

            component.GetSyntheticStringMemberOrDefault("missing", "fallback");
            component.GetSyntheticBooleanMemberOrDefault("missing", true);
            component.GetSyntheticInt32MemberOrDefault("missing", 7);
            component.GetSyntheticSingleMemberOrDefault("missing", 7f);

            Assert.Null(ReadSyntheticBag(component, "SyntheticStringMembers"));
            Assert.Null(ReadSyntheticBag(component, "SyntheticBooleanMembers"));
            Assert.Null(ReadSyntheticBag(component, "SyntheticInt32Members"));
            Assert.Null(ReadSyntheticBag(component, "SyntheticSingleMembers"));

            component.SetSyntheticStringMember("name", "value");
            component.SetSyntheticBooleanMember("flag", true);
            component.SetSyntheticInt32Member("count", 3);
            component.SetSyntheticSingleMember("ratio", 0.5f);

            Assert.NotNull(ReadSyntheticBag(component, "SyntheticStringMembers"));
            Assert.NotNull(ReadSyntheticBag(component, "SyntheticBooleanMembers"));
            Assert.NotNull(ReadSyntheticBag(component, "SyntheticInt32Members"));
            Assert.NotNull(ReadSyntheticBag(component, "SyntheticSingleMembers"));
        }

        [Fact]
        public void Synthetic_member_accessors_round_trip_values_and_fall_back_to_the_caller_default() {
            Component component = new Component();

            Assert.Equal("fallback", component.GetSyntheticStringMemberOrDefault("name", "fallback"));
            Assert.Equal(string.Empty, component.GetSyntheticStringMemberOrDefault("name", null));
            Assert.True(component.GetSyntheticBooleanMemberOrDefault("flag", true));
            Assert.Equal(7, component.GetSyntheticInt32MemberOrDefault("count", 7));
            Assert.Equal(0.25f, component.GetSyntheticSingleMemberOrDefault("ratio", 0.25f));

            component.SetSyntheticStringMember("name", "value");
            component.SetSyntheticBooleanMember("flag", false);
            component.SetSyntheticInt32Member("count", 3);
            component.SetSyntheticSingleMember("ratio", 0.5f);

            Assert.Equal("value", component.GetSyntheticStringMemberOrDefault("name", "fallback"));
            Assert.False(component.GetSyntheticBooleanMemberOrDefault("flag", true));
            Assert.Equal(3, component.GetSyntheticInt32MemberOrDefault("count", 7));
            Assert.Equal(0.5f, component.GetSyntheticSingleMemberOrDefault("ratio", 0.25f));
        }

        [Fact]
        public void Setting_a_null_string_member_stores_the_empty_string() {
            Component component = new Component();

            component.SetSyntheticStringMember("name", null);

            Assert.Equal(string.Empty, component.GetSyntheticStringMemberOrDefault("name", "fallback"));
        }

        [Fact]
        public void Synthetic_member_accessors_reject_blank_member_names() {
            Component component = new Component();

            Assert.Throws<ArgumentException>(() => component.SetSyntheticStringMember(" ", "value"));
            Assert.Throws<ArgumentException>(() => component.SetSyntheticBooleanMember(" ", true));
            Assert.Throws<ArgumentException>(() => component.SetSyntheticInt32Member(" ", 1));
            Assert.Throws<ArgumentException>(() => component.SetSyntheticSingleMember(" ", 1f));
            Assert.Throws<ArgumentException>(() => component.GetSyntheticStringMemberOrDefault(" ", "value"));
            Assert.Throws<ArgumentException>(() => component.GetSyntheticBooleanMemberOrDefault(" ", true));
            Assert.Throws<ArgumentException>(() => component.GetSyntheticInt32MemberOrDefault(" ", 1));
            Assert.Throws<ArgumentException>(() => component.GetSyntheticSingleMemberOrDefault(" ", 1f));
        }

        /// <summary>
        /// Reads one synthetic member bag through reflection so the lazy allocation can be observed without widening the public surface.
        /// </summary>
        /// <param name="component">Component whose bag should be inspected.</param>
        /// <param name="fieldName">Backing field name of the bag.</param>
        /// <returns>Bag instance when allocated; otherwise null.</returns>
        static object ReadSyntheticBag(Component component, string fieldName) {
            FieldInfo field = typeof(Component).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return field.GetValue(component);
        }
    }
}
