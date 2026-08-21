// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var months = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'
]

function normalizePublicationDate(value) {
  if (value === undefined || value === null || value === '') {
    return null
  }

  var text = String(value)
  var match = /^(\d{4})-(\d{2})-(\d{2})/.exec(text)

  if (!match) {
    return {
      iso: text,
      display: text
    }
  }

  var month = Number(match[2])
  var day = Number(match[3])

  return {
    iso: match[1] + '-' + match[2] + '-' + match[3],
    display: months[month - 1] + ' ' + day + ', ' + match[1]
  }
}

exports.postTransform = function (model) {
  if (!model.author || !model.published) {
    return model
  }

  var published = normalizePublicationDate(model.published)
  var updated = normalizePublicationDate(model.updated)

  model._hasPublicationMetadata = true
  model._publicationPublishedIso = published.iso
  model._publicationPublishedDisplay = published.display

  if (updated) {
    model._publicationUpdatedIso = updated.iso
    model._publicationUpdatedDisplay = updated.display
  }

  return model
}
