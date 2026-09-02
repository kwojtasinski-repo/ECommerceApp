define("coupon-rule-builder", ["ajaxRequest"], function (ajaxRequestModule) {
    var ajaxRequest = ajaxRequestModule.ajaxRequest;
    function initialize(root) {
        var catalog = JSON.parse(root.dataset.catalog || "{}");
        // Polish labels come from the view (CouponRuleDisplayNames) — the schema's Label stays English.
        var parameterLabels = JSON.parse(root.dataset.parameterLabels || "{}");
        var categories = catalog.categories || catalog.Categories || [];
        var rules = categories ? categories.reduce(function (all, category) {
            return all.concat(category.rules || category.Rules || []);
        }, []) : [];
        var selects = root.querySelectorAll(".rule-select");
        selects.forEach(function (select) {
            select.addEventListener("change", function () {
                renderParameters(root, select.dataset.slot, select.value, rules, 0, parameterLabels);
                if (select.dataset.slot === "scope") {
                    toggleTargetPicker(root, select.value, rules);
                }
            });
        });

        root.querySelector("#addConstraint").addEventListener("click", function () {
            var select = root.querySelector("#constraintRuleSelect");
            if (!select.value) {
                return;
            }

            var index = root.querySelectorAll("[data-constraint]").length;
            var wrapper = document.createElement("div");
            wrapper.dataset.constraint = "true";
            wrapper.className = "border rounded p-2 mb-2";
            wrapper.innerHTML = "<input type=\"hidden\" name=\"Rules.Constraints[" + index + "].Name\" value=\"" + select.value + "\"><strong>" + select.options[select.selectedIndex].text + "</strong><button type=\"button\" class=\"btn btn-outline-secondary btn-sm float-end\">" + (root.dataset.removeLabel || "") + "</button><div data-parameters=\"constraint-" + index + "\"></div>";
            root.querySelector("#constraintRules").appendChild(wrapper);
            wrapper.querySelector("button").addEventListener("click", function () { wrapper.remove(); });
            renderParameters(wrapper, "constraint-" + index, select.value, rules, index, parameterLabels);
            select.value = "";
        });
    }

    function renderParameters(root, slot, name, rules, constraintIndex, parameterLabels) {
        var container = root.querySelector("[data-parameters='" + slot + "']");
        if (!container) {
            return;
        }

        container.innerHTML = "";
        var rule = rules.find(function (item) { return (item.name || item.Name) === name; });
        ((rule && (rule.parameters || rule.Parameters)) || []).forEach(function (parameter) {
            var key = parameter.key || parameter.Key;
            var type = parameter.type || parameter.Type;
            var fieldName = slot.indexOf("constraint-") === 0
                ? "Rules.Constraints[" + constraintIndex + "].Parameters[" + key + "]"
                : "Rules." + slot.charAt(0).toUpperCase() + slot.slice(1) + ".Parameters[" + key + "]";
            var input = document.createElement("input");
            input.className = "form-control mb-2";
            input.name = fieldName;
            input.type = type === "Date" ? "date" : type === "String" ? "text" : "number";
            input.value = parameter.defaultValue || parameter.DefaultValue || "";
            input.required = parameter.required || parameter.Required;
            if (parameter.min !== null && parameter.min !== undefined) { input.min = parameter.min; }
            if (parameter.Min !== null && parameter.Min !== undefined) { input.min = parameter.Min; }
            if (parameter.max !== null && parameter.max !== undefined) { input.max = parameter.max; }
            if (parameter.Max !== null && parameter.Max !== undefined) { input.max = parameter.Max; }
            var label = document.createElement("label");
            label.className = "form-label";
            label.textContent = (parameterLabels && parameterLabels[key]) || key;
            container.appendChild(label);
            container.appendChild(input);
        });
    }

    function toggleTargetPicker(root, scopeName, rules) {
        var rule = rules.find(function (item) { return (item.name || item.Name) === scopeName; });
        var picker = root.querySelector("#scopeTargetPicker");
        picker.hidden = !(rule && (rule.requiresScopeTargets || rule.RequiresScopeTargets));
        if (!picker.hidden) {
            loadTargets(root, scopeName, "");
        }
    }

    function loadTargets(root, scopeType, query) {
        var url = root.dataset.targetUrl + "?scopeType=" + encodeURIComponent(scopeType) + "&query=" + encodeURIComponent(query);
        ajaxRequest.send(url, "GET", null, null, "json").then(function (items) {
            var select = root.querySelector("#scopeTargetSelect");
            // Placeholder text stays in the view (see _RuleBuilder.cshtml) — reuse the markup's own
            // first option rather than duplicating a Polish string here.
            select.innerHTML = "";
            var placeholder = document.createElement("option");
            placeholder.value = "";
            placeholder.textContent = root.dataset.targetPlaceholder || "";
            select.appendChild(placeholder);
            items.forEach(function (item) {
                var option = document.createElement("option");
                option.value = item.id;
                option.textContent = item.name;
                option.dataset.name = item.name;
                select.appendChild(option);
            });
            select.onchange = function () { setScopeTarget(root, scopeType, select); };
        });
    }

    function setScopeTarget(root, scopeType, select) {
        var option = select.options[select.selectedIndex];
        var parameterKey = scopeType === "per-product" ? "productId" : scopeType === "per-category" ? "categoryId" : "tagId";
        var input = root.querySelector("[name='Rules.Scope.Parameters[" + parameterKey + "]']");
        if (input) { input.value = select.value; }
        root.querySelector("#scopeTargetFields").innerHTML = "<input type=\"hidden\" name=\"ScopeTargets[0].ScopeType\" value=\"" + scopeType + "\"><input type=\"hidden\" name=\"ScopeTargets[0].TargetId\" value=\"" + select.value + "\"><input type=\"hidden\" name=\"ScopeTargets[0].TargetName\" value=\"" + (option.dataset.name || "") + "\">";
    }

    return { initialize: initialize };
});