define([], function () {
    function prefillFromProfile(profileUrl) {
        fetch(profileUrl, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (profile) {
                if (!profile) {
                    return;
                }

                setField('CustomerId', profile.customerId);
                setField('FirstName', profile.firstName);
                setField('LastName', profile.lastName);
                setField('Email', profile.email);
                setField('PhoneNumber', profile.phoneNumber);
                setCheck('IsCompany', profile.isCompany);
                setField('CompanyName', profile.companyName);
                setField('Nip', profile.nip);
                setField('Street', profile.street);
                setField('BuildingNumber', profile.buildingNumber);
                setField('FlatNumber', profile.flatNumber);
                setField('ZipCode', profile.zipCode);
                setField('City', profile.city);
                setField('Country', profile.country);
            })
            .catch(function () { /* silent fail — user fills the form manually */ });
    }

    function setField(id, value) {
        var el = document.getElementById(id);
        if (el && value != null) el.value = value;
    }

    function setCheck(id, value) {
        var el = document.getElementById(id);
        if (el) el.checked = value === true;
    }

    return { prefillFromProfile: prefillFromProfile };
});
